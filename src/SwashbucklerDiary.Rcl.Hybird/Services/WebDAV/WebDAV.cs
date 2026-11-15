using System.Net;
using System.Text;
using WebDav;

namespace SwashbucklerDiary.Rcl.Services
{
    public class WebDAV : IWebDAV
    {
        private WebDavClient? webDavClient;

        /// <summary>
        /// 与 <see cref="webDavClient"/> 共用同一个实例。同步的 PUT 需要原样发送
        /// <c>If-Match</c>（坚果云 ETag 不带引号），WebDav.Client 的强类型 header 会拒绝，
        /// 只能绕到原始 HttpClient 上。
        /// </summary>
        private HttpClient? httpClient;

        private const string webDavFolderName = "SwashbucklerDiary";

        public bool Initialized { get; set; }

        public async Task<Stream> DownloadAsync(string destFileName)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var response = await webDavClient.GetRawFile(destFileName);
            if (response.IsSuccessful)
            {
                return response.Stream;
            }
            else
            {
                throw new WebDAVException(response.ToString());
            }
        }

        public async Task<List<WebDAVFileInfo>> GetZipFileListAsync(string folderName)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.Propfind($"{webDavFolderName}/");
            if (result.IsSuccessful)
            {
                return result.Resources
                    .Where(it => !it.IsCollection)
                    .Where(it => it.Uri.EndsWith(".zip"))
                    .OrderByDescending(it => it.LastModifiedDate)
                    .Select(it => new WebDAVFileInfo()
                    {
                        Name  = Uri.UnescapeDataString(Path.GetFileName(it.Uri)),
                        Length = it.ContentLength,
                        LastModified = it.LastModifiedDate
                    })
                    .ToList();
            }
            else
            {
                throw new WebDAVException(result.ToString());
            }
        }

        public async Task Set(string? baseAddress, string? userName, string? password)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseAddress, nameof(baseAddress));
            ArgumentException.ThrowIfNullOrWhiteSpace(userName, nameof(userName));
            ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));

            bool uriResult = Uri.TryCreate(baseAddress, UriKind.Absolute, out Uri? uri);
            if (!uriResult || uri is null)
            {
                throw new ArgumentException(null, nameof(baseAddress));
            }

            var webDavClient = GetWebDavClient(uri, userName, password, out var client);
            var result = await webDavClient.Propfind($"{webDavFolderName}/");
            if (!result.IsSuccessful)
            {
                if (result.StatusCode != (int)HttpStatusCode.NotFound)
                {
                    throw new WebDAVException(result.ToString());
                }

                var result2 = await webDavClient.Mkcol(webDavFolderName);
                if (!result2.IsSuccessful)
                {
                    throw new WebDAVException(result2.ToString());
                }
            }

            this.webDavClient = webDavClient;
            this.httpClient = client;
            Initialized = true;
        }

        public async Task UploadAsync(string destFileName, Stream stream)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.PutFile(destFileName, stream);
            if (!result.IsSuccessful)
            {
                throw new WebDAVException(result.ToString());
            }
        }

        // ---- 同步专用 ----

        public async Task<WebDavFileResult?> GetFileAsync(string path, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            // 坚果云的 GET 响应不暴露 ETag（WebDav.Client 的 GetRawFile 也不返回响应头），
            // 只能先用 PROPFIND 取一次。
            var propfind = await webDavClient.Propfind(path, new PropfindParameters { CancellationToken = ct });
            if (!propfind.IsSuccessful)
            {
                if (propfind.StatusCode == 404)
                {
                    return null;
                }

                throw new WebDAVException(propfind.ToString());
            }

            string? etag = propfind.Resources
                .FirstOrDefault(it => !it.IsCollection)?
                .ETag;

            var response = await webDavClient.GetRawFile(path, new GetFileParameters { CancellationToken = ct });
            if (!response.IsSuccessful)
            {
                if (response.StatusCode == 404)
                {
                    return null;
                }

                throw new WebDAVException(response.ToString());
            }

            return new WebDavFileResult
            {
                Stream = response.Stream,
                ETag = etag,
            };
        }

        public async Task<int> PutFileAsync(string path, Stream stream, string? ifMatch = null, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(httpClient, nameof(httpClient));

            using var request = new HttpRequestMessage(HttpMethod.Put, path);
            // 用自定义 Content 而不是 StreamContent：StreamContent 被 Dispose 时会关闭底层流，
            // 而同步的重试逻辑（阶段 5 附件）需要复用同一个流。
            request.Content = new LeaveOpenStreamContent(stream);
            request.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            if (!string.IsNullOrEmpty(ifMatch))
            {
                // If-Match 按 RFC 7232 必须是带引号的 entity-tag，但坚果云返回的 ETag 不带引号。
                // WebDav.Client 内部走强类型 header 解析，会在客户端抛 FormatException；
                // 这里绕到原始 HttpClient，用 TryAddWithoutValidation 原样发送。
                if (!request.Headers.TryAddWithoutValidation("If-Match", ifMatch))
                {
                    // 加不上就意味着这次写没有并发保护，绝不能静默继续。
                    throw new InvalidOperationException($"Invalid ETag for If-Match: {ifMatch}");
                }
            }

            using var response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            return (int)response.StatusCode;
        }

        public async Task DeleteFileAsync(string path, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.Delete(path, new DeleteParameters { CancellationToken = ct });
            if (!result.IsSuccessful && result.StatusCode != 404)
            {
                throw new WebDAVException(result.ToString());
            }
        }

        public async Task MakeCollectionAsync(string path, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.Mkcol(path, new MkColParameters { CancellationToken = ct });
            // 405：已存在；301：部分服务端用重定向表示集合已存在。
            if (!result.IsSuccessful && result.StatusCode is not (405 or 301))
            {
                throw new WebDAVException(result.ToString());
            }
        }

        public async Task<string?> GetETagAsync(string path, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.Propfind(path, new PropfindParameters { CancellationToken = ct });
            if (!result.IsSuccessful)
            {
                if (result.StatusCode == 404)
                {
                    return null;
                }

                throw new WebDAVException(result.ToString());
            }

            return result.Resources
                .FirstOrDefault(it => !it.IsCollection)?
                .ETag;
        }

        public async Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.Propfind(path, new PropfindParameters { CancellationToken = ct });
            if (result.IsSuccessful)
            {
                return true;
            }

            if (result.StatusCode == 404)
            {
                return false;
            }

            throw new WebDAVException(result.ToString());
        }

        public async Task<List<WebDavEntry>> ListAsync(string path, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(webDavClient, nameof(webDavClient));

            var result = await webDavClient.Propfind(path, new PropfindParameters { CancellationToken = ct });
            if (!result.IsSuccessful)
            {
                if (result.StatusCode == 404)
                {
                    return [];
                }

                throw new WebDAVException(result.ToString());
            }

            var entries = new List<WebDavEntry>();
            foreach (var resource in result.Resources)
            {
                string name = Uri.UnescapeDataString(
                    Path.GetFileName(resource.Uri.TrimEnd('/')));
                if (string.IsNullOrEmpty(name))
                {
                    // 目录本身
                    continue;
                }

                entries.Add(new WebDavEntry(
                    name,
                    resource.Uri,
                    resource.ContentLength,
                    resource.LastModifiedDate,
                    resource.ETag,
                    resource.IsCollection));
            }

            return entries;
        }

        private static WebDavClient GetWebDavClient(Uri uri, string userName, string password, out HttpClient httpClient)
        {
            var httpHandler = new SocketsHttpHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                Credentials = new NetworkCredential(userName, password)
            };

            httpClient = new HttpClient(httpHandler, true)
            {
                BaseAddress = uri,
                // 同步元数据用 2 分钟足够；阶段 5 的附件上传需要按文件大小单独放宽。
                Timeout = TimeSpan.FromMinutes(2),
            };

            // 预置 Basic 认证。否则首次请求会先吃一个 401，重试时 PUT 的 StreamContent
            // 可能已经读完，导致 0 字节上传。
            string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            return new WebDavClient(httpClient);
        }

        /// <summary>
        /// 把流包成 <see cref="HttpContent"/>，但 <see cref="Dispose(bool)"/> 不关闭底层流。
        /// <para>
        /// 直接用 <see cref="StreamContent"/> 时，请求释放会连带关闭调用方的流；
        /// 阶段 5 的附件重试会复用同一个流，必须由调用方掌握生命周期。
        /// </para>
        /// </summary>
        private sealed class LeaveOpenStreamContent : HttpContent
        {
            private readonly Stream _stream;

            /// <summary>可 seek 时预先算长度，避免 HttpClient 退化成 chunked 上传。</summary>
            private readonly long? _length;

            public LeaveOpenStreamContent(Stream stream)
            {
                _stream = stream;
                _length = stream.CanSeek ? stream.Length - stream.Position : null;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
                => _stream.CopyToAsync(stream);

            protected override bool TryComputeLength(out long length)
            {
                length = _length ?? 0;

                // 不可 seek 的流返回 false，HttpClient 会退化成 chunked PUT。
                // 阶段 0 的探测用的都是已知长度的 MemoryStream，坚果云是否接受 chunked
                // 尚未验证；阶段 5 上传附件前要么复制到可 seek 缓冲，要么先测一次。
                return _length.HasValue;
            }

            protected override void Dispose(bool disposing)
            {
                // 故意不释放 _stream。
            }
        }
    }
}
