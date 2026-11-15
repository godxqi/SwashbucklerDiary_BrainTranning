namespace SwashbucklerDiary.Rcl.Services
{
    public interface IWebDAV
    {
        bool Initialized { get; protected set; }

        Task Set(string? baseAddress, string? userName, string? password);

        Task<Stream> DownloadAsync(string destFileName);

        Task UploadAsync(string destFileName, Stream stream);

        Task<List<WebDAVFileInfo>> GetZipFileListAsync(string folderName);

        // ---- 同步专用：文件级读写与 ETag 条件请求 ----

        /// <summary>读取文件与远端 ETag；文件不存在返回 <see langword="null"/>。</summary>
        Task<WebDavFileResult?> GetFileAsync(string path, CancellationToken ct = default);

        /// <summary>
        /// 写入文件。<paramref name="ifMatch"/> 非空时带 <c>If-Match</c> 条件写，
        /// 返回原始状态码（412 表示另一台设备抢先提交）。
        /// <para>流的所有权归调用方，方法返回后流仍然可读。</para>
        /// </summary>
        Task<int> PutFileAsync(string path, Stream stream, string? ifMatch = null, CancellationToken ct = default);

        Task DeleteFileAsync(string path, CancellationToken ct = default);

        /// <summary>创建集合；已存在（405/301）视为成功。</summary>
        Task MakeCollectionAsync(string path, CancellationToken ct = default);

        /// <summary>取单个文件的远端 ETag；不存在返回 <see langword="null"/>。</summary>
        Task<string?> GetETagAsync(string path, CancellationToken ct = default);

        Task<bool> ExistsAsync(string path, CancellationToken ct = default);

        Task<List<WebDavEntry>> ListAsync(string path, CancellationToken ct = default);
    }
}
