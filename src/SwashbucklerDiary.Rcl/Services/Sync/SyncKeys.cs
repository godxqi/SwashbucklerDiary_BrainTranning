namespace SwashbucklerDiary.Rcl.Services
{
    public static class SyncKeys
    {
        /// <summary>同步状态 JSON 的 Preferences 键。</summary>
        public const string StateKey = "SyncState";

        /// <summary>是否在启动时自动同步。</summary>
        public const string AutoOnStartKey = "SyncAutoOnStart";

        /// <summary>是否同步附件（默认关闭，避免占用坚果云免费流量）。</summary>
        public const string IncludeAssetsKey = "SyncIncludeAssets";
    }
}
