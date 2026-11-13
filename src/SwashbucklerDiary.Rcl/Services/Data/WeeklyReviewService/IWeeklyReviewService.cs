using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 周课（方法文档 2）。文档说「这一节是全系统的心脏」：
    /// 2.1 把这一周的微观失败人工归类、人工判性；2.2 最多升一条；2.3 定三个目标；
    /// 2.4 结算上周三个目标；2.5 四处出口检查；2.6 本周一件新事物。
    /// </summary>
    public interface IWeeklyReviewService
    {
        /// <summary>某一周的周课记录。没有就是没做过，返回 null。</summary>
        Task<WeeklyReviewModel?> GetAsync(int year, int weekOfYear);

        /// <summary>取（必要时建）<paramref name="date"/> 所在那一周的周课记录。</summary>
        Task<WeeklyReviewModel> GetOrCreateAsync(DateTime date);

        /// <summary>把 2.4 的追问 / 2.5 四处出口 / 2.6 新事物写回日记。</summary>
        Task<bool> SaveAsync(WeeklyReviewModel review, WeeklyReviewSections sections);

        /// <summary>读出某一周的散文。那一周没写过就是一份空章节，不是 null。</summary>
        Task<WeeklyReviewSections> GetSectionsAsync(int year, int weekOfYear);

        /// <summary>
        /// 立「这一周的周课做完了」的旗。节拍判定（需求 14）只认这一个开关——
        /// 不立旗的话复盘主页会一直把它算成已逾期。
        /// </summary>
        Task<bool> FinalizeAsync(WeeklyReviewModel review, WeeklyReviewSections sections);

        /// <summary>某一周定的目标。该周没有周课记录时返回空表——那是「上周没定目标」，不是错误。</summary>
        Task<List<WeeklyGoalModel>> GetGoalsAsync(int year, int weekOfYear);

        /// <summary>
        /// 归档区 7.2「对未来的新计划」的入口（文档 7.2）：周课 2.3 定下、<b>跨周还没做</b>的目标。
        /// 判定就是「那一周已经过完了，目标还没有变成达成或放弃」。
        /// </summary>
        Task<List<WeeklyGoalModel>> GetCarriedOverGoalsAsync(DateTime beforeDate);

        Task<bool> SaveGoalAsync(WeeklyGoalModel goal);

        Task<bool> DeleteGoalAsync(Guid id);

        /// <summary>
        /// 2.1 结算一个目标（达成 / 未达成二选一）。达成 → 进成就列表（6.3）；
        /// 未达成的归因（哪几项 SMART 出了问题、各自为什么）由页面写在
        /// <see cref="WeeklyGoalModel"/> 的五个 <c>SmartXxxNote</c> 上，这里一并落库。
        /// <para>
        /// <b>2026-10-06 起不再写 7.4 归档</b>：未达成不再单独问「是不是环境原因」，
        /// 那条路改由 SMART 的 A 项承担归因（只归因、不归档）。
        /// </para>
        /// <para>
        /// <paramref name="settledDate"/> 是「正在做的那一周」里的任意一天（由页面传它当前看的那一周）。
        /// 早先这里取的是 <c>DateTime.Now</c>，于是<b>回过去补某一周的周课时，结算会被记成「现在这一周」</b>。
        /// </para>
        /// </summary>
        Task<bool> SettleGoalAsync(WeeklyGoalModel goal, DateTime settledDate, GoalStatus status);

        /// <summary>
        /// 周课页 ⋮ 里的「跳过这一周 / 撤销跳过」。只动 <see cref="WeeklyReviewModel.IsSkipped"/> 这一个旗，
        /// 不碰内容；一写东西就自动取消（见 <see cref="SaveAsync"/>）。
        /// </summary>
        Task<bool> SetSkippedAsync(WeeklyReviewModel review, bool skipped);
    }
}
