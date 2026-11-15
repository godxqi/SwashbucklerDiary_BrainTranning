namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 同步存储抽象。路径一律用 <c>/</c> 分隔、相对于同步根目录，例如
    /// <c>manifest.json</c>、<c>diaries/{id}.json</c>、<c>assets/Image/{sha256}.jpg</c>。
    /// </summary>
    public interface ISyncStorage
    {
        /// <summary>读取文件；不存在时返回 <see langword="null"/>（而不是抛异常）。</summary>
        Task<SyncFile?> GetAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// 写入文件。<paramref name="ifMatch"/> 非空时走条件写，
        /// 远端 ETag 不匹配返回 <see cref="SyncWriteResult.PreconditionFailed"/>。
        /// </summary>
        Task<SyncWriteResult> PutAsync(string path, Stream content, string? ifMatch = null, CancellationToken ct = default);

        Task DeleteAsync(string path, CancellationToken ct = default);

        /// <summary>递归创建目录，已存在视为成功。</summary>
        Task EnsureDirectoryAsync(string path, CancellationToken ct = default);

        Task<IReadOnlyList<SyncEntry>> ListAsync(string path, CancellationToken ct = default);

        Task<bool> ExistsAsync(string path, CancellationToken ct = default);
    }

    public sealed class SyncFile
    {
        public required Stream Content { get; init; }

        /// <summary>远端 ETag，原样保存，提交时原样回传（坚果云不带引号，不能加工）。</summary>
        public string? ETag { get; init; }
    }

    public sealed class SyncWriteResult
    {
        public required int StatusCode { get; init; }

        public bool IsSuccess => StatusCode is 200 or 201 or 204;

        /// <summary>HTTP 412：<c>If-Match</c> 不匹配，说明另一台设备抢先提交了。</summary>
        public bool PreconditionFailed => StatusCode == 412;
    }

    public sealed record SyncEntry(
        string Name,
        long? Length,
        DateTimeOffset? LastModified,
        string? ETag,
        bool IsDirectory);
}
