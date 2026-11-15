namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 本机同步快照。由阶段 3 从数据库/文件系统采集，diff 引擎只读它，不碰数据库和网络。
    /// </summary>
    public sealed class SyncLocalSnapshot
    {
        /// <summary>key 为日记 Id（<c>guid "N"</c>）。</summary>
        public Dictionary<string, SyncObjectMeta> Diaries { get; init; } = [];

        /// <summary>key 为复盘表短名。</summary>
        public Dictionary<string, SyncObjectMeta> Review { get; init; } = [];

        public SyncObjectMeta? Locations { get; init; }

        /// <summary>key 为 <c>{类型}/{文件名}</c>。</summary>
        public Dictionary<string, SyncObjectMeta> Assets { get; init; } = [];

        /// <summary>
        /// 本地墓碑。<b>key 必须带类型前缀</b>（用 <see cref="SyncEntityKeys"/> 拼），
        /// 值为删除时间 Unix 毫秒。
        /// </summary>
        public Dictionary<string, long> Graves { get; init; } = [];
    }
}
