namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 执行 <see cref="SyncPlan"/>。只负责"按计划做事"，不做 diff、不提交 manifest。
    /// </summary>
    public interface ISyncExecutor
    {
        /// <summary>
        /// 执行计划，并把成功处理的对象元数据写回 <paramref name="remote"/>（远端 manifest 的内存副本）。
        /// 单个对象失败会记录到 <see cref="SyncExecuteResult.Errors"/> 并继续，不阻断整体同步。
        /// </summary>
        Task<SyncExecuteResult> ExecuteAsync(
            SyncPlan plan,
            SyncManifest remote,
            SyncLocalSnapshot local,
            SyncState state,
            CancellationToken ct = default);
    }

    public sealed class SyncExecuteResult
    {
        public int Uploaded { get; set; }

        public int Downloaded { get; set; }

        public int DeletedLocal { get; set; }

        public int DeletedRemote { get; set; }

        public int Conflicts { get; set; }

        /// <summary>阶段 3 尚未接入的实体类型（复盘/附件/地点）。</summary>
        public int Skipped { get; set; }

        public List<string> Errors { get; } = [];
    }
}
