using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>人物页用：一行日课事件，外加它所属日课的日期。</summary>
    public sealed record DailyEventView(DailyEventModel Event, DateTime Date);

    /// <summary>
    /// 日课 1.1「生活上的」的行。一行一件事，可挂一个人物标签。
    /// </summary>
    public interface IDailyEventService
    {
        Task<List<DailyEventModel>> QueryByReviewAsync(Guid dailyReviewId);

        /// <summary>用给出的行整体替换这次日课的 1.1；空内容行会被跳过。</summary>
        Task<bool> SaveAsync(Guid dailyReviewId, IReadOnlyList<DailyEventModel> events);

        Task<bool> DeleteByReviewAsync(Guid dailyReviewId);

        /// <summary>人物页用：某个人的所有日课片段，按日课时间从新到旧。</summary>
        Task<List<DailyEventView>> QueryByPersonAsync(Guid personTagId);
    }
}
