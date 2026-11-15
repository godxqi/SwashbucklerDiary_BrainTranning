namespace SwashbucklerDiary.Rcl.Services
{
    public enum SyncEntityType
    {
        Diary,
        Review,
        Asset,
        Location,
    }

    public enum SyncAction
    {
        /// <summary>本地较新或远端缺失，需要推送。</summary>
        Upload,

        /// <summary>远端较新或本地缺失，需要拉取。</summary>
        Download,

        /// <summary>两端在同一个同步窗口内都改过，先备份本地为副本再应用远端。</summary>
        Conflict,

        /// <summary>远端墓碑生效，删除本地对象。</summary>
        DeleteLocal,

        /// <summary>本地墓碑生效，删除远端对象。</summary>
        DeleteRemote,
    }

    /// <summary>
    /// 墓碑与计划条目用的统一键。
    /// <para>
    /// 两端的字符串必须逐字节一致，否则墓碑认不出来、被删数据会"复活"。
    /// 所以拼键只允许走这里，不要在业务代码里手写。
    /// </para>
    /// </summary>
    public static class SyncEntityKeys
    {
        public const string DiaryType = "diary";

        public const string ReviewType = "review";

        public const string AssetType = "asset";

        public const string DiaryPrefix = DiaryType + ":";

        public const string ReviewPrefix = ReviewType + ":";

        public const string AssetPrefix = AssetType + ":";

        /// <summary>地点整表对象的键（manifest.Locations）。</summary>
        public const string LocationKey = "location";

        /// <summary>地点行墓碑的前缀：<c>location:{rowId}</c>。</summary>
        public const string LocationPrefix = LocationKey + ":";

        public static string Diary(Guid id) => DiaryPrefix + id.ToString("N");

        public static string Review(string tableKey) => ReviewPrefix + tableKey;

        public static string Asset(string path) => AssetPrefix + path;

        public static SyncEntityType GetType(string key)
        {
            if (key.StartsWith(DiaryPrefix, StringComparison.Ordinal)) return SyncEntityType.Diary;
            if (key.StartsWith(ReviewPrefix, StringComparison.Ordinal)) return SyncEntityType.Review;
            if (key.StartsWith(AssetPrefix, StringComparison.Ordinal)) return SyncEntityType.Asset;
            if (key == LocationKey || key.StartsWith(LocationPrefix, StringComparison.Ordinal))
            {
                return SyncEntityType.Location;
            }

            throw new ArgumentException($"Unknown sync entity key: {key}", nameof(key));
        }

        /// <summary>把统一键还原成 manifest 字典里的裸 key。</summary>
        public static string GetBareKey(string key) => GetType(key) switch
        {
            SyncEntityType.Diary => key[DiaryPrefix.Length..],
            SyncEntityType.Review => key[ReviewPrefix.Length..],
            SyncEntityType.Asset => key[AssetPrefix.Length..],
            SyncEntityType.Location => key.StartsWith(LocationPrefix, StringComparison.Ordinal)
                ? key[LocationPrefix.Length..]
                : string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };
    }
}
