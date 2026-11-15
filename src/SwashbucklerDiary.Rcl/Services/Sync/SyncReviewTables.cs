using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 复盘表类型与 manifest/文件名用的短键映射。
    /// <para>
    /// 键必须跨版本稳定：它同时出现在 <c>manifest.json</c>、<c>review.json</c> 和行墓碑里，
    /// 改名等于让旧数据的墓碑失效。
    /// </para>
    /// </summary>
    public static class SyncReviewTables
    {
        /// <summary>复盘整体只用一个同步对象（<c>review.json</c>），行级差异在文件内部合并。</summary>
        public const string AllKey = "all";

        private static readonly Dictionary<Type, string> _keys = new()
        {
            [typeof(DailyReviewModel)] = "daily",
            [typeof(WeeklyReviewModel)] = "weekly",
            [typeof(WeeklyGoalModel)] = "weeklyGoal",
            [typeof(StumblingBlockModel)] = "stumblingBlock",
            [typeof(MonthlyReviewModel)] = "monthly",
            [typeof(LimitingBeliefModel)] = "limitingBelief",
            [typeof(EnemyModel)] = "enemy",
            [typeof(VirtueEntryModel)] = "virtueEntry",
            [typeof(SelfAffirmationModel)] = "affirmation",
            [typeof(ArchiveItemModel)] = "archiveItem",
            [typeof(MacroGoalModel)] = "macroGoal",
            [typeof(StateRecordModel)] = "stateRecord",
            [typeof(LessonModel)] = "lesson",
            [typeof(LessonTypeLinkModel)] = "lessonTypeLink",
            [typeof(DailyEventModel)] = "dailyEvent",
        };

        public static string? GetKey(Type type) => _keys.GetValueOrDefault(type);
    }
}
