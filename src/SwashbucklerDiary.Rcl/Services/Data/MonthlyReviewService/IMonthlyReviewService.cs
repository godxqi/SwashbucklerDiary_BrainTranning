using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 月课四步的闸门状态。文档要求「每步要有产出才放行」——
    /// 月记做完如果没有出口，就只是「写完了」，所以每一步都得留下东西。
    /// </summary>
    public sealed record MonthlyStepGates(bool Step31, bool Step32, bool Step33, bool Step34)
    {
        public bool AllDone => Step31 && Step32 && Step33 && Step34;

        public static readonly MonthlyStepGates None = new(false, false, false, false);
    }

    /// <summary>
    /// 月课（方法文档 3）。四步是带闸门的向导：
    /// <list type="number">
    /// <item>3.1 三列审视（限制性观念 / 支持性证据 / 反面证据 / 效用），最多六条</item>
    /// <item>3.2 出口：把 3.1 第三列的反对性陈述提炼成短语，进自我肯定库（3–4 条）</item>
    /// <item>3.3 自我补偿——<b>不许跳过</b>，这是全系统唯一一个专门挖痛处的动作</item>
    /// <item>3.4 三列三行敌人表（针对<b>目标</b>，与 3.1 针对<b>信念</b>不同）</item>
    /// </list>
    /// <para>
    /// 月课没有自由散文：四步的产出全是结构化的，日记正文由
    /// <see cref="MonthlyReviewSections"/> 把产出渲染成可读摘要写进受管区块。
    /// </para>
    /// </summary>
    public interface IMonthlyReviewService
    {
        /// <summary>某一个月的月课记录。没有就是没开过，返回 null。</summary>
        Task<MonthlyReviewModel?> GetAsync(int year, int month);

        /// <summary>取（必要时建）<paramref name="date"/> 所在那个月的月课记录。</summary>
        Task<MonthlyReviewModel> GetOrCreateAsync(DateTime date);

        /// <summary>3.1 三列审视的行，按录入顺序。</summary>
        Task<List<LimitingBeliefModel>> GetBeliefsAsync(Guid monthlyReviewId);

        Task<bool> SaveBeliefAsync(LimitingBeliefModel belief);

        Task<bool> DeleteBeliefAsync(Guid id);

        /// <summary>3.4 敌人表的行，按录入顺序。</summary>
        Task<List<EnemyModel>> GetEnemiesAsync(Guid monthlyReviewId);

        Task<bool> SaveEnemyAsync(EnemyModel enemy);

        Task<bool> DeleteEnemyAsync(Guid id);

        /// <summary>
        /// 3.2 的出口：把一条反对性陈述提炼成短语写进自我肯定库。
        /// 写进去的条目会带上 <see cref="SelfAffirmationModel.SourceMonthlyReviewId"/>，
        /// 所以「这一次月课产出过几条」查得出来——那正是 3.2 的闸门。
        /// </summary>
        Task<bool> AddAffirmationAsync(MonthlyReviewModel review, string? phrase, string? source);

        /// <summary>
        /// 重算四步闸门（只算不写）。页面用它决定「下一步」能不能点——
        /// 没产出就点不动，并且要告诉使用者还差什么。
        /// </summary>
        Task<MonthlyStepGates> GetGatesAsync(MonthlyReviewModel? review);

        /// <summary>写 3.3 的产出、重算闸门、把摘要渲染进日记正文。</summary>
        Task<bool> SaveAsync(MonthlyReviewModel review, string? step33Note);

        /// <summary>
        /// 月课页 ⋮ 里的「跳过这一月 / 撤销跳过」。只动 <see cref="MonthlyReviewModel.IsSkipped"/> 这个旗，
        /// 不碰内容；一写东西就自动取消（见 <see cref="SaveAsync"/>）。
        /// </summary>
        Task<bool> SetSkippedAsync(MonthlyReviewModel review, bool skipped);

        /// <summary>
        /// 立「本月月课做完了」的旗。节拍判定（需求 14）只认这一个开关——
        /// <see cref="IReviewScheduleService.GetTodayTasksAsync"/> 已经在读它了，
        /// 不立旗复盘主页会把这一月永远算成「已逾期」。
        /// </summary>
        Task<bool> FinalizeAsync(MonthlyReviewModel review, string? step33Note);
    }
}
