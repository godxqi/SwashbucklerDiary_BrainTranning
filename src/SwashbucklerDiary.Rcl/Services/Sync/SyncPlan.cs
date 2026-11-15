namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// diff 引擎的输出。纯数据，不执行任何 IO；阶段 3 按它执行上传/下载/删除。
    /// </summary>
    public sealed class SyncPlan
    {
        public List<SyncChange> Changes { get; } = [];

        /// <summary>
        /// 需要写入远端 manifest 的本地墓碑。
        /// <para>
        /// 带上删除时间是刻意的：manifest 的 <c>graves</c> 要写 <c>key → deletedAt</c>，
        /// 只给 key 会逼执行层回查快照，计划本身就不自洽了。
        /// </para>
        /// </summary>
        public List<SyncGraveEntry> GravesToPublish { get; } = [];

        /// <summary>本地对象比远端墓碑新，需要从 manifest 撤销的墓碑（统一键）。</summary>
        public List<string> GravesToWithdraw { get; } = [];

        public bool HasChanges
            => Changes.Count > 0 || GravesToPublish.Count > 0 || GravesToWithdraw.Count > 0;

        internal void Add(SyncEntityType type, string key, SyncAction action, long localUpdateTime, long remoteUpdateTime)
        {
            Changes.Add(new SyncChange(type, key, action, localUpdateTime, remoteUpdateTime));
        }
    }

    public sealed record SyncChange(
        SyncEntityType EntityType,
        string Key,
        SyncAction Action,
        long LocalUpdateTime,
        long RemoteUpdateTime);

    public sealed record SyncGraveEntry(string Key, long DeletedAt);
}
