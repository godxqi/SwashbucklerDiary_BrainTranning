namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 同步自检。与业务服务分开，避免把诊断方法塞进 <see cref="ISyncGraveService"/> 这类生产接口。
    /// </summary>
    public interface ISyncDiagnosticsService
    {
        /// <summary>
        /// 验证 AOP 是否真的会在整实体更新时刷新 <c>UpdateTime</c>。
        /// AOP 失效不会报错，只会让增量同步永远认为"没有变化"，所以必须在真机上验一次。
        /// </summary>
        Task<SyncProbeStep> ProbeUpdateTimeAopAsync(CancellationToken ct = default);
    }
}
