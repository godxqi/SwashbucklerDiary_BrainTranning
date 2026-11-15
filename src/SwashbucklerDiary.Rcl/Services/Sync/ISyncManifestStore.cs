namespace SwashbucklerDiary.Rcl.Services
{
    public interface ISyncManifestStore
    {
        /// <summary>读取远端 manifest；不存在返回 <see langword="null"/>。</summary>
        Task<SyncManifestSnapshot?> ReadAsync(CancellationToken ct = default);

        /// <summary>
        /// 写入远端 manifest。<paramref name="ifMatch"/> 为读取时拿到的 ETag，
        /// 返回 412 表示另一台设备抢先提交。
        /// </summary>
        Task<SyncWriteResult> WriteAsync(SyncManifest manifest, string? ifMatch, CancellationToken ct = default);
    }

    public sealed class SyncManifestSnapshot
    {
        public required SyncManifest Manifest { get; init; }

        public string? ETag { get; init; }
    }
}
