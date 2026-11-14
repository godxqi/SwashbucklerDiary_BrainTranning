using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// JSON 导出里的复盘部分，落到包内的 <c>review.json</c>。
    /// <para>
    /// 为什么需要它：<c>ExportJsonAsync</c> 只序列化 <see cref="DiaryModel"/>，
    /// 复盘表一条都不在导出里——用 JSON 换设备会把节拍、目标、归档、状态记录、课题全丢光。
    /// 整库备份（<c>ExportDBAsync</c>）复制的是整个 .db3，所以天然没这个缺口，缺的只是可读导出。
    /// </para>
    /// <para>
    /// <b>装的是全量，不是导出日记的子集。</b>这些表是一张互相咬合的图
    /// （目标 → 周课 → 月课 → 肯定库 → 归档 → 状态记录），任何子集规则都会静默切断引用；
    /// 而多带几条数据的代价只是包大一点，少带几条的代价是换设备后回环断掉。
    /// </para>
    /// <para>
    /// ⚠️ <b>这带来一个要留意的副作用</b>：JSON 导出支持按时间 / 标签 / 单篇导出子集
    /// （<c>ExportByTimeDialog</c>、<c>DiaryCardList</c> 等），而复盘部分**永远是全量**——
    /// 于是「只导出 2026 年 1 月」这个包，里面带着全部状态记录（身体 / 心理 / 情感 / 精神的原文）
    /// 与全部自我肯定。方向上是「宁多勿少」（少带会踩到 <c>SourceDiaryId</c> 为空的回流行
    /// 和手动归档，那是真丢数据），但对「按子集导出后把这个包发给别人」这个用法，
    /// 它拿到的比预期多。
    /// <b>阶段 5 待办</b>：在导出界面上给一句提示说清楚这件事。
    /// </para>
    /// </summary>
    public class ReviewExportModel
    {
        public List<DailyReviewModel> DailyReviews { get; set; } = [];

        public List<WeeklyReviewModel> WeeklyReviews { get; set; } = [];

        public List<WeeklyGoalModel> WeeklyGoals { get; set; } = [];

        public List<StumblingBlockModel> StumblingBlocks { get; set; } = [];

        public List<MonthlyReviewModel> MonthlyReviews { get; set; } = [];

        public List<LimitingBeliefModel> LimitingBeliefs { get; set; } = [];

        public List<EnemyModel> Enemies { get; set; } = [];

        public List<VirtueEntryModel> VirtueEntries { get; set; } = [];

        public List<SelfAffirmationModel> Affirmations { get; set; } = [];

        public List<ArchiveItemModel> ArchiveItems { get; set; } = [];

        public List<MacroGoalModel> MacroGoals { get; set; } = [];

        public List<StateRecordModel> StateRecords { get; set; } = [];

        public List<LessonModel> Lessons { get; set; } = [];

        public List<LessonTypeLinkModel> LessonTypeLinks { get; set; } = [];

        public List<DailyEventModel> DailyEvents { get; set; } = [];

        /// <summary>一条复盘数据都没有——空包不值得写一个 review.json 出去。</summary>
        public bool IsEmpty => DailyReviews.Count == 0
            && WeeklyReviews.Count == 0
            && WeeklyGoals.Count == 0
            && StumblingBlocks.Count == 0
            && MonthlyReviews.Count == 0
            && LimitingBeliefs.Count == 0
            && Enemies.Count == 0
            && VirtueEntries.Count == 0
            && Affirmations.Count == 0
            && ArchiveItems.Count == 0
            && MacroGoals.Count == 0
            && StateRecords.Count == 0
            && Lessons.Count == 0
            && LessonTypeLinks.Count == 0
            && DailyEvents.Count == 0;
    }
}
