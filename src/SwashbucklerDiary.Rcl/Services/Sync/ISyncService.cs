namespace SwashbucklerDiary.Rcl.Services
{
    public interface ISyncService
    {
        /// <summary>
        /// 执行一次双向同步：读远端 manifest → 采集本地快照 → diff → 执行 → CAS 提交。
        /// 同一时刻只允许一个同步或自检，后来的调用会立即返回 <see cref="SyncResult.Busy"/>。
        /// </summary>
        Task<SyncResult> SyncAsync(CancellationToken ct = default);

        /// <summary>
        /// 同步自检：在同步目录里跑一遍写入、读回、ETag、条件写、删除。
        /// 阶段 6 会挂在同步页的"诊断"入口上。
        /// </summary>
        Task<SyncProbeResult> ProbeAsync(CancellationToken ct = default);
    }

    public sealed class SyncProbeResult
    {
        public List<SyncProbeStep> Steps { get; } = [];

        public bool AllPassed => Steps.Count > 0 && Steps.All(it => it.Passed);

        public void Add(string name, bool passed, string detail)
            => Steps.Add(new SyncProbeStep(name, passed, detail));
    }

    public sealed record SyncProbeStep(string Name, bool Passed, string Detail);
}
