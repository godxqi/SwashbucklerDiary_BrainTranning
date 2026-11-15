namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 采集本机同步快照。只读：不修改数据库，也不访问网络。
    /// </summary>
    public interface ISyncSnapshotService
    {
        Task<SyncLocalSnapshot> CaptureAsync(CancellationToken ct = default);
    }
}
