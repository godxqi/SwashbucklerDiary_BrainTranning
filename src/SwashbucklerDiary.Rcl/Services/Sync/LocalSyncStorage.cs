using System.Security.Cryptography;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 把同步存储落到本地目录。用途只有一个：在没有网的情况下模拟"两台设备"跑集成测试。
    /// <para>
    /// ETag 用文件内容的 SHA256 前 16 位十六进制模拟，语义与 WebDAV 一致：
    /// 写入后 ETag 变化、<c>If-Match</c> 不匹配返回 412。
    /// </para>
    /// </summary>
    public class LocalSyncStorage : ISyncStorage
    {
        private readonly string _root;

        public LocalSyncStorage(string root)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(root, nameof(root));
            _root = Path.GetFullPath(root);
        }

        public Task<SyncFile?> GetAsync(string path, CancellationToken ct = default)
        {
            string fullPath = Resolve(path);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult<SyncFile?>(null);
            }

            // 一次性读进内存：调用方拿到的流不应该被文件句柄绑住，否则并发读写会互相锁死。
            byte[] bytes = File.ReadAllBytes(fullPath);
            var file = new SyncFile
            {
                Content = new MemoryStream(bytes, writable: false),
                ETag = ComputeETag(bytes),
            };
            return Task.FromResult<SyncFile?>(file);
        }

        public async Task<SyncWriteResult> PutAsync(string path, Stream content, string? ifMatch = null, CancellationToken ct = default)
        {
            string fullPath = Resolve(path);
            string? currentETag = File.Exists(fullPath)
                ? ComputeETag(await File.ReadAllBytesAsync(fullPath, ct).ConfigureAwait(false))
                : null;

            if (ifMatch is not null && !string.Equals(ifMatch, currentETag, StringComparison.Ordinal))
            {
                return new SyncWriteResult { StatusCode = 412 };
            }

            bool existed = File.Exists(fullPath);

            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using (var target = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(target, ct).ConfigureAwait(false);
            }

            return new SyncWriteResult { StatusCode = existed ? 204 : 201 };
        }

        public Task DeleteAsync(string path, CancellationToken ct = default)
        {
            string fullPath = Resolve(path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
            }

            return Task.CompletedTask;
        }

        public Task EnsureDirectoryAsync(string path, CancellationToken ct = default)
        {
            Directory.CreateDirectory(Resolve(path));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncEntry>> ListAsync(string path, CancellationToken ct = default)
        {
            string fullPath = Resolve(path);
            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult<IReadOnlyList<SyncEntry>>([]);
            }

            var entries = new List<SyncEntry>();
            foreach (string item in Directory.EnumerateFileSystemEntries(fullPath))
            {
                var info = new FileInfo(item);
                bool isDirectory = Directory.Exists(item);
                entries.Add(new SyncEntry(
                    Path.GetFileName(item),
                    isDirectory ? null : info.Length,
                    info.LastWriteTimeUtc,
                    isDirectory ? null : ComputeETag(File.ReadAllBytes(item)),
                    isDirectory));
            }

            return Task.FromResult<IReadOnlyList<SyncEntry>>(entries);
        }

        public Task<bool> ExistsAsync(string path, CancellationToken ct = default)
        {
            string fullPath = Resolve(path);
            return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
        }

        /// <summary>
        /// 把相对路径解析到根目录内。必须挡住 <c>..</c> 和绝对路径——
        /// 同步路径来自远端 manifest，不能让它写到根目录外面去。
        /// </summary>
        private string Resolve(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path, nameof(path));

            string relative = path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(_root, relative));

            // 必须带上分隔符再比较：只比 "C:\sync" 的话，"C:\sync-evil" 也会通过。
            string rootWithSeparator = Path.EndsInDirectorySeparator(_root)
                ? _root
                : _root + Path.DirectorySeparatorChar;

            if (!string.Equals(full, _root, StringComparison.OrdinalIgnoreCase)
                && !full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Path escapes sync root: {path}", nameof(path));
            }

            return full;
        }

        private static string ComputeETag(byte[] bytes)
        {
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 8));
        }
    }
}
