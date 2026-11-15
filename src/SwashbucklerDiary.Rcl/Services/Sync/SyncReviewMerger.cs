using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 复盘数据的行级合并。
    /// <para>
    /// 复盘是一张互相咬合的图（目标 → 周课 → 月课 → 归档 → 状态记录），整包 LWW 会成片丢数据，
    /// 所以按行 Id 做 LWW：两端各改不同行时都保留，改同一行时时间新的胜出。
    /// </para>
    /// </summary>
    public static class SyncReviewMerger
    {
        public static ReviewExportModel Merge(
            ReviewExportModel local,
            ReviewExportModel remote,
            IReadOnlyDictionary<string, long> graves)
        {
            return new ReviewExportModel
            {
                DailyReviews = MergeRows(local.DailyReviews, remote.DailyReviews, "daily", graves),
                WeeklyReviews = MergeRows(local.WeeklyReviews, remote.WeeklyReviews, "weekly", graves),
                WeeklyGoals = MergeRows(local.WeeklyGoals, remote.WeeklyGoals, "weeklyGoal", graves),
                StumblingBlocks = MergeRows(local.StumblingBlocks, remote.StumblingBlocks, "stumblingBlock", graves),
                MonthlyReviews = MergeRows(local.MonthlyReviews, remote.MonthlyReviews, "monthly", graves),
                LimitingBeliefs = MergeRows(local.LimitingBeliefs, remote.LimitingBeliefs, "limitingBelief", graves),
                Enemies = MergeRows(local.Enemies, remote.Enemies, "enemy", graves),
                VirtueEntries = MergeRows(local.VirtueEntries, remote.VirtueEntries, "virtueEntry", graves),
                Affirmations = MergeRows(local.Affirmations, remote.Affirmations, "affirmation", graves),
                ArchiveItems = MergeRows(local.ArchiveItems, remote.ArchiveItems, "archiveItem", graves),
                MacroGoals = MergeRows(local.MacroGoals, remote.MacroGoals, "macroGoal", graves),
                StateRecords = MergeRows(local.StateRecords, remote.StateRecords, "stateRecord", graves),
                Lessons = MergeRows(local.Lessons, remote.Lessons, "lesson", graves),
                LessonTypeLinks = MergeRows(local.LessonTypeLinks, remote.LessonTypeLinks, "lessonTypeLink", graves),
                DailyEvents = MergeRows(local.DailyEvents, remote.DailyEvents, "dailyEvent", graves),
            };
        }

        public static List<T> MergeRows<T>(
            IReadOnlyList<T> local,
            IReadOnlyList<T> remote,
            string tableKey,
            IReadOnlyDictionary<string, long> graves,
            string? gravePrefix = null)
            where T : BaseModel
        {
            // 复盘行的墓碑键是 {review:}{table}:{rowId}；地点用 location:{rowId}，所以前缀可覆盖。
            string keyPrefix = gravePrefix ?? $"{SyncEntityKeys.ReviewPrefix}{tableKey}:";
            var merged = new Dictionary<Guid, T>();

            foreach (var row in local)
            {
                merged[row.Id] = row;
            }

            foreach (var row in remote)
            {
                if (!merged.TryGetValue(row.Id, out var existing) || row.UpdateTime > existing.UpdateTime)
                {
                    merged[row.Id] = row;
                }
            }

            // 行墓碑：删除时间不早于该行的更新时间才生效，否则视为删除后又被编辑（复活）。
            foreach (var row in merged.Values.ToList())
            {
                string graveKey = keyPrefix + row.Id.ToString("N");
                if (!graves.TryGetValue(graveKey, out long deletedAt))
                {
                    continue;
                }

                long rowTime = SyncTime.ToUnixMs(row.UpdateTime == default ? row.CreateTime : row.UpdateTime);
                if (rowTime <= deletedAt)
                {
                    merged.Remove(row.Id);
                }
            }

            // 排序保证序列化哈希稳定。
            return merged.Values.OrderBy(it => it.Id).ToList();
        }

        /// <summary>整包的最后修改时间：取所有行里的最大值。</summary>
        public static long MaxUpdateTime(ReviewExportModel model)
        {
            long max = 0;
            max = Math.Max(max, MaxOf(model.DailyReviews));
            max = Math.Max(max, MaxOf(model.WeeklyReviews));
            max = Math.Max(max, MaxOf(model.WeeklyGoals));
            max = Math.Max(max, MaxOf(model.StumblingBlocks));
            max = Math.Max(max, MaxOf(model.MonthlyReviews));
            max = Math.Max(max, MaxOf(model.LimitingBeliefs));
            max = Math.Max(max, MaxOf(model.Enemies));
            max = Math.Max(max, MaxOf(model.VirtueEntries));
            max = Math.Max(max, MaxOf(model.Affirmations));
            max = Math.Max(max, MaxOf(model.ArchiveItems));
            max = Math.Max(max, MaxOf(model.MacroGoals));
            max = Math.Max(max, MaxOf(model.StateRecords));
            max = Math.Max(max, MaxOf(model.Lessons));
            max = Math.Max(max, MaxOf(model.LessonTypeLinks));
            max = Math.Max(max, MaxOf(model.DailyEvents));
            return max;
        }

        public static long MaxOf<T>(IEnumerable<T> rows) where T : BaseModel
            => rows.Select(it => SyncTime.ToUnixMs(it.UpdateTime == default ? it.CreateTime : it.UpdateTime))
                .DefaultIfEmpty(0)
                .Max();
    }
}
