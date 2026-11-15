namespace SwashbucklerDiary.Rcl.Services
{
    public sealed class SyncResult
    {
        public bool Success { get; init; }

        /// <summary>已有同步/自检在进行，本次没有执行。</summary>
        public bool Busy { get; init; }

        /// <summary>两端都没有变化。</summary>
        public bool NoChanges { get; init; }

        public string? Message { get; init; }

        public int Uploaded { get; init; }

        public int Downloaded { get; init; }

        public int DeletedLocal { get; init; }

        public int DeletedRemote { get; init; }

        public int Conflicts { get; init; }

        public List<string> Errors { get; init; } = [];

        public static SyncResult BusyResult()
            => new() { Busy = true, Message = "已有同步或自检正在进行" };

        public static SyncResult NoChangesResult()
            => new() { Success = true, NoChanges = true, Message = "没有需要同步的改动" };

        public static SyncResult Failed(string message)
            => new() { Success = false, Message = message };

        public static SyncResult Completed(SyncExecuteResult execute)
            => new()
            {
                Success = true,
                Uploaded = execute.Uploaded,
                Downloaded = execute.Downloaded,
                DeletedLocal = execute.DeletedLocal,
                DeletedRemote = execute.DeletedRemote,
                Conflicts = execute.Conflicts,
                Errors = execute.Errors,
                Message = execute.Errors.Count == 0
                    ? null
                    : $"{execute.Errors.Count} 个对象同步失败，下次同步会重试",
            };
    }
}
