namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>文件内容 + 远端 ETag。坚果云的 PUT 响应不带 ETag，所以 ETag 只能通过 PROPFIND 单独取。</summary>
    public sealed class WebDavFileResult
    {
        public required Stream Stream { get; init; }

        /// <summary>原样保存，提交时原样回传。坚果云返回的 ETag 不带双引号，不要按 RFC 加工。</summary>
        public string? ETag { get; init; }
    }

    public sealed record WebDavEntry(
        string Name,
        string Uri,
        long? Length,
        DateTime? LastModified,
        string? ETag,
        bool IsDirectory);
}
