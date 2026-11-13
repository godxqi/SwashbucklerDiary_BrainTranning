using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>一条复盘任务的当前状态。</summary>
    public enum ReviewTaskState
    {
        /// <summary>在节拍窗口内，还没做。</summary>
        Pending,

        /// <summary>已经结算过。</summary>
        Done,

        /// <summary>错过了宽容期，还没补。</summary>
        Overdue,
    }

    /// <summary>
    /// 复盘主页「今天该做什么」的一条。节拍（需求 14）：日课每天、周课锚定周五宽容至周日、
    /// 月课锚定月末宽容至次月 3 号。
    /// </summary>
    public record ReviewTask(
        ReviewKind Kind,
        DateTime PeriodStart,
        DateTime GraceEndDate,
        ReviewTaskState State);

    /// <summary>
    /// 节拍判定的<b>唯一真源</b>。复盘主页上的「今天该做什么」只认这里，
    /// 别在页面里另算一套周次/月次。
    /// </summary>
    public interface IReviewScheduleService
    {
        /// <summary>ISO 周（周一 ~ 周日）。周五锚点与周日宽容期都落在这个区间里。</summary>
        (DateTime Start, DateTime End, int Year, int WeekOfYear) GetWeek(DateTime date);

        (DateTime Start, DateTime End, int Year, int Month) GetMonth(DateTime date);

        /// <summary>
        /// 复盘主页要显示的三条（含已完成的）。三种节拍各自的判定是它的内部实现——
        /// 外面只关心「今天该做什么」，分开问反而容易各算一套周次。
        /// </summary>
        Task<List<ReviewTask>> GetTodayTasksAsync(DateTime today);
    }
}
