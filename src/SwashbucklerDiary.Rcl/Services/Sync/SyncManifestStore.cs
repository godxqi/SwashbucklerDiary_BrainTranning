using System.Text.Json;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncManifestStore : ISyncManifestStore
    {
        public const string ManifestPath = "manifest.json";

        private readonly ISyncStorage _storage;

        public SyncManifestStore(ISyncStorage storage)
        {
            _storage = storage;
        }

        public async Task<SyncManifestSnapshot?> ReadAsync(CancellationToken ct = default)
        {
            var file = await _storage.GetAsync(ManifestPath, ct).ConfigureAwait(false);
            if (file is null)
            {
                return null;
            }

            await using var content = file.Content;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct).ConfigureAwait(false);

            var manifest = JsonSerializer.Deserialize(
                buffer.ToArray(),
                SyncJsonContext.Default.SyncManifest);

            return new SyncManifestSnapshot
            {
                Manifest = manifest ?? new SyncManifest(),
                ETag = file.ETag,
            };
        }

        public async Task<SyncWriteResult> WriteAsync(
            SyncManifest manifest,
            string? ifMatch,
            CancellationToken ct = default)
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
                manifest,
                SyncJsonContext.Default.SyncManifest);

            // 必须 await 之后才让 using 释放流：直接 return Task 的话，
            // 方法先返回、流立刻被 Dispose，异步写入会读到已关闭的流。
            using var stream = new MemoryStream(bytes, writable: false);
            return await _storage.PutAsync(ManifestPath, stream, ifMatch, ct).ConfigureAwait(false);
        }
    }
}
