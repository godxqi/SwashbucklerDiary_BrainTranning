using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncAssetHandler : ISyncAssetHandler
    {
        /// <summary>坚果云免费版单文件上限约 500 MB。超过就跳过并记录，不阻断整轮同步。</summary>
        private const long MaxAssetBytes = 500L * 1024 * 1024;

        private const int Concurrency = 4;

        private readonly ISyncStorage _storage;

        private readonly IMediaResourceManager _mediaResourceManager;

        private readonly ILogger<SyncAssetHandler> _logger;

        public SyncAssetHandler(
            ISyncStorage storage,
            IMediaResourceManager mediaResourceManager,
            ILogger<SyncAssetHandler> logger)
        {
            _storage = storage;
            _mediaResourceManager = mediaResourceManager;
            _logger = logger;
        }

        public async Task ExecuteAsync(
            IReadOnlyList<SyncChange> changes,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct)
        {
            if (changes.Count == 0)
            {
                return;
            }

            int uploaded = 0;
            int downloaded = 0;
            int deletedLocal = 0;
            int deletedRemote = 0;
            var errors = new ConcurrentBag<string>();

            await Parallel.ForEachAsync(
                changes,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Concurrency,
                    CancellationToken = ct,
                },
                async (change, token) =>
                {
                    try
                    {
                        string bareKey = SyncEntityKeys.GetBareKey(change.Key);
                        switch (change.Action)
                        {
                            case SyncAction.Upload:
                                await UploadAsync(bareKey, remote, token).ConfigureAwait(false);
                                Interlocked.Increment(ref uploaded);
                                break;

                            case SyncAction.Download:
                                await DownloadAsync(bareKey, token).ConfigureAwait(false);
                                Interlocked.Increment(ref downloaded);
                                break;

                            case SyncAction.DeleteLocal:
                                DeleteLocal(bareKey);
                                Interlocked.Increment(ref deletedLocal);
                                break;

                            case SyncAction.DeleteRemote:
                                await _storage.DeleteAsync(
                                    SyncAssetPath.ToRemotePath(bareKey), token).ConfigureAwait(false);
                                lock (remote.Assets)
                                {
                                    remote.Assets.Remove(bareKey);
                                }

                                Interlocked.Increment(ref deletedRemote);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{change.Key}: {ex.Message}");
                        _logger.LogError(ex, "Asset sync failed: {Key} {Action}", change.Key, change.Action);
                    }
                }).ConfigureAwait(false);

            result.Uploaded += uploaded;
            result.Downloaded += downloaded;
            result.DeletedLocal += deletedLocal;
            result.DeletedRemote += deletedRemote;

            foreach (string error in errors)
            {
                result.Errors.Add(error);
            }
        }

        private async Task UploadAsync(string bareKey, SyncManifest remote, CancellationToken ct)
        {
            string? localPath = SyncAssetPath.ResolveLocal(_mediaResourceManager, bareKey);
            if (localPath is null || !File.Exists(localPath))
            {
                return;
            }

            if (new FileInfo(localPath).Length > MaxAssetBytes)
            {
                throw new InvalidOperationException(
                    $"附件超过 {MaxAssetBytes / 1024 / 1024} MB，已跳过");
            }

            await using (var stream = File.OpenRead(localPath))
            {
                var write = await _storage
                    .PutAsync(SyncAssetPath.ToRemotePath(bareKey), stream, null, ct)
                    .ConfigureAwait(false);

                if (!write.IsSuccess)
                {
                    throw new InvalidOperationException($"上传失败：HTTP {write.StatusCode}");
                }
            }

            string hash = SyncAssetPath.TryGetHashFromFileName(localPath, out string fileNameHash)
                ? fileNameHash
                : await ComputeFileHashAsync(localPath, ct).ConfigureAwait(false);

            // 附件是并发处理的，Dictionary 本身不线程安全，写回元数据必须加锁。
            lock (remote.Assets)
            {
                remote.Assets[bareKey] = new SyncObjectMeta
                {
                    UpdateTime = SyncTime.ToUnixMs(File.GetLastWriteTimeUtc(localPath)),
                    Hash = hash,
                };
            }
        }

        private async Task DownloadAsync(string bareKey, CancellationToken ct)
        {
            string? localPath = SyncAssetPath.ResolveLocal(_mediaResourceManager, bareKey);
            if (localPath is null)
            {
                throw new ArgumentException($"无法解析附件路径：{bareKey}");
            }

            var file = await _storage
                .GetAsync(SyncAssetPath.ToRemotePath(bareKey), ct)
                .ConfigureAwait(false);

            if (file is null)
            {
                return;
            }

            string? directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 上传有大小限制，下载也必须有限制：远端一条坏数据不能把磁盘写满。
            long total = 0;
            await using (file.Content)
            await using (var target = File.Create(localPath))
            {
                byte[] buffer = new byte[81920];
                int read;
                while ((read = await file.Content.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxAssetBytes)
                    {
                        target.Close();
                        File.Delete(localPath);
                        throw new InvalidOperationException(
                            $"附件超过 {MaxAssetBytes / 1024 / 1024} MB，已中止下载");
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                }
            }
        }

        private void DeleteLocal(string bareKey)
        {
            string? localPath = SyncAssetPath.ResolveLocal(_mediaResourceManager, bareKey);
            if (localPath is not null && File.Exists(localPath))
            {
                File.Delete(localPath);
            }
        }

        private static async Task<string> ComputeFileHashAsync(string path, CancellationToken ct)
        {
            await using var stream = File.OpenRead(path);
            byte[] bytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
