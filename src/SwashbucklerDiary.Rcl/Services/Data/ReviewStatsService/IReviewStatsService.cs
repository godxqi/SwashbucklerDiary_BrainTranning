namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 一段时间的节拍完成度。<see cref="Total"/> 为 0 表示这段时间里压根没有该做的节拍
    /// （比如刚用第一天就来看统计），页面要显示成「—」而不是 0%。
    /// </summary>
    /// <param name="Done">已完成的周期数（周期 = 一天 / 一周 / 一月）。</param>
    /// <param name="Total">该做的周期数。<b>已经扣掉「跳过且没写」的那些</b>。</param>
    /// <param name="Skipped">被扣掉的那部分有几条，只为了让页面能解释「分母为什么变小了」。</param>
    public sealed record PeriodStat(int Done, int Total, int Skipped = 0)
    {
        public bool HasData => Total > 0;

        public int Percent => Total == 0 ? 0 : (int)Math.Round(Done * 100.0 / Total);
    }

    /// <summary>排行榜里的一条。</summary>
    public sealed record RankedItem(string Label, int Count);

    /// <summary>
    /// 统计页（需求 16：节拍完成度 + 高频模式）的聚合。
    /// <para>
    /// 只收「别处拿不到」的四个数：触因排行直接用
    /// <see cref="ITriggerService.GetWithCountsAsync"/>、成就增长直接用
    /// <see cref="IVirtueService.GetAchievementGrowthAsync"/>，这里不重复包一层。
    /// </para>
    /// <para>
    /// 全部走 SQL 聚合，不在 C# 里遍历全表（复刻 <c>TagRepository.TagsDiaryCount()</c> 的范式）。
    /// 唯一的例外是「跳过」的级联扣除：它要按天判断这一天落在哪个被跳过的周/月里，
    /// 只能在 C# 侧跑一遍区间（区间是 30~365 天，有界）。
    /// </para>
    /// <para>
    /// <b>跳过（<c>IsSkipped</c>）的扣除规则</b>：某个周期「跳过且没写」时才从分母里扣掉；
    /// 写过了就照常计入分母（分子里也有它）。级联方向是<b>月 → 周 → 天</b>：
    /// 跳过的月把它里面的周与天也带上，但跳过的周不会反过来盖住整个月。
    /// </para>
    /// </summary>
    public interface IReviewStatsService
    {
        /// <summary>
        /// 日课完成度。分母只算<b>已经过去</b>的日子——今天还没做不算欠，未来更不算。
        /// </summary>
        Task<PeriodStat> GetDailyCompletionAsync(DateTime fromInclusive, DateTime toExclusive);

        /// <summary>周课完成度。只算已经走完的 ISO 周；本周还在宽容期里，不计入分母。</summary>
        Task<PeriodStat> GetWeeklyCompletionAsync(DateTime fromInclusive, DateTime toExclusive);

        /// <summary>月课完成度。只算已经走完的自然月。</summary>
        Task<PeriodStat> GetMonthlyCompletionAsync(DateTime fromInclusive, DateTime toExclusive);

        /// <summary>
        /// 失败排行：这段时间里出现次数最多的微观失败。次数高的排前面。
        /// <para>
        /// 失败行只有 <c>Year</c> / <c>WeekOfYear</c> 没有日期，所以时间范围先换算成
        /// ISO 周的区间，再用 <c>Year * 100 + WeekOfYear</c> 这个单调的合成键去比大小——
        /// 这样仍然是 SQL 侧的过滤，不用拉全表回内存。
        /// </para>
        /// </summary>
        Task<List<RankedItem>> GetFailureRankingAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            int top = 5);
    }
}
