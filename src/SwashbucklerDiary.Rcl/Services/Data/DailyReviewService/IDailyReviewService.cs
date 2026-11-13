using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 日课（方法文档 1）的读写。
    /// 一次日课 = 一条 <see cref="DailyReviewModel"/>（结构化面：情绪 / 精力 / 肯定 / 微观失败镜像）
    /// + 一条真实 <see cref="DiaryModel"/>（散文面，日历 / 搜索 / 导出 / 隐私模式全部继承）。
    /// </summary>
    public interface IDailyReviewService
    {
        Task<DailyReviewModel?> GetByDateAsync(DateTime date);

        /// <summary>取那一天背后的日记，用于把用户写过的东西回填到表单。</summary>
        Task<DiaryModel?> GetDiaryByDateAsync(DateTime date);

        /// <summary>
        /// 保存。<see cref="DailyReviewSections"/> 渲染成按章节号排好的正文，
        /// 芯片的产物追加到末尾的受管区块。两者互不覆盖。
        /// </summary>
        Task<bool> SaveAsync(DailyReviewModel review, DailyReviewSections sections);

        /// <summary>
        /// 删掉某一天的日课：结构化的那行、它派生的失败行、<b>以及那篇日记</b>。
        /// 删完那一天就回到「没写过」——复盘主页回到待办、统计扣掉。
        /// <para>
        /// 只在用户显式点「删除这次日课」时调用（页面带确认框）。把表单清空<b>不会</b>走这里，
        /// 因为清空只是「不写了」，不等于「这天不存在」。
        /// </para>
        /// </summary>
        Task<bool> DeleteAsync(DateTime date);

        /// <summary>历史上有日课的日子（去重），给日课页日历上的「哪天写了」标点用。</summary>
        Task<List<DateTime>> GetReviewDatesAsync();

        /// <summary>
        /// 取某一周（ISO 周，周一 ~ 周日）的全部日课，按日期升序。
        /// 给「本周日课总览」页用：散文从日记正文解析，情绪与肯定短语从结构化字段取。
        /// <paramref name="date"/> 只要落在目标周内即可。
        /// </summary>
        Task<DailyReviewWeekOverview> GetWeekOverviewAsync(DateTime date);
    }
}
