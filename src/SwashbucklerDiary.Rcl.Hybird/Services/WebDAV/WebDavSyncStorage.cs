namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 把 <see cref="ISyncStorage"/> 落到坚果云 WebDAV。
    /// <para>
    /// 所有同步对象都放在 <c>SwashbucklerDiary/Sync/</c> 下，与现有的整库备份 zip 并列，
    /// 互不影响——备份是"整库覆盖"，同步是"对象增删"，两条路不会互相踩。
    /// </para>
    /// </summary>
    public class WebDavSyncStorage : ISyncStorage
    {
        private const string Root = "SwashbucklerDiary/Sync";

        private readonly IWebDAV _webDav;

        public WebDavSyncStorage(IWebDAV webDav)
        {
            _webDav = webDav;
        }

        public async Task<SyncFile?> GetAsync(string path, CancellationToken ct = default)
        {
            var file = await _webDav.GetFileAsync(Full(path), ct).ConfigureAwait(false);
            if (file is null)
            {
                return null;
            }

            return new SyncFile
            {
                Content = file.Stream,
                ETag = file.ETag,
            };
        }

        public async Task<SyncWriteResult> PutAsync(string path, Stream content, string? ifMatch = null, CancellationToken ct = default)
        {
            int statusCode = await _webDav.PutFileAsync(Full(path), content, ifMatch, ct)
                .ConfigureAwait(false);
            return new SyncWriteResult { StatusCode = statusCode };
        }

        public Task DeleteAsync(string path, CancellationToken ct = default)
            => _webDav.DeleteFileAsync(Full(path), ct);

        public async Task EnsureDirectoryAsync(string path, CancellationToken ct = default)
        {
            // WebDAV 只能一级一级建，且中间层已存在时返回 405，MakeCollectionAsync 已把 405 当成功。
            string current = Root;
            foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                current += "/" + segment;
                await _webDav.MakeCollectionAsync(current, ct).ConfigureAwait(false);
            }
        }

        public async Task<IReadOnlyList<SyncEntry>> ListAsync(string path, CancellationToken ct = default)
        {
            var entries = await _webDav.ListAsync(Full(path), ct).ConfigureAwait(false);
            return entries
                .Select(it => new SyncEntry(
                    it.Name,
                    it.Length,
                    it.LastModified is DateTime dt ? new DateTimeOffset(dt) : null,
                    it.ETag,
                    it.IsDirectory))
                .ToList();
        }

        public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
            => _webDav.ExistsAsync(Full(path), ct);

        /// <summary>
        /// 把同步相对路径拼成远端完整路径。
        /// <para>
        /// 同步路径大量来自远端 manifest，属于外部输入。这里必须挡住 <c>..</c> 和绝对路径，
        /// 否则一条被污染的 manifest 就能把数据写到 <c>SwashbucklerDiary/Sync/</c> 之外。
        /// </para>
        /// </summary>
        private static string Full(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.Contains("..", StringComparison.Ordinal)
                || path[0] == '/'
                || path.Contains('\\', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Invalid sync path: {path}", nameof(path));
            }

            return $"{Root}/{path}";
        }
    }
}
