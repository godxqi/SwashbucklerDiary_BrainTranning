using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 一周日课的总览数据（「本周日课总览」页只读它）。
    /// 按天收齐：散文来自日记正文的解析结果，情绪与肯定短语来自结构化字段。
    /// </summary>
    public sealed record DailyReviewWeekOverview(
        DateTime WeekStart,
        DateTime WeekEnd,
        IReadOnlyList<DailyReviewDayOverview> Days);

    /// <summary>总览里的一天。</summary>
    public sealed record DailyReviewDayOverview(
        DateTime Date,
        DailyReviewSections Sections,
        MoodKind? Mood,
        IReadOnlyList<string> Affirmations);
}
