using SqlSugar;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class ReviewStatsService : IReviewStatsService
    {
        private readonly IReviewRepository<DailyReviewModel> _dailyRepository;

        private readonly IReviewRepository<WeeklyReviewModel> _weeklyRepository;

        private readonly IReviewRepository<MonthlyReviewModel> _monthlyRepository;

        private readonly IReviewRepository<StumblingBlockModel> _stumblingBlockRepository;

        private readonly IReviewScheduleService _reviewScheduleService;

        public ReviewStatsService(IReviewRepository<DailyReviewModel> dailyRepository,
            IReviewRepository<WeeklyReviewModel> weeklyRepository,
            IReviewRepository<MonthlyReviewModel> monthlyRepository,
            IReviewRepository<StumblingBlockModel> stumblingBlockRepository,
            IReviewScheduleService reviewScheduleService)
        {
            _dailyRepository = dailyRepository;
            _weeklyRepository = weeklyRepository;
            _monthlyRepository = monthlyRepository;
            _stumblingBlockRepository = stumblingBlockRepository;
            _reviewScheduleService = reviewScheduleService;
        }

        public async Task<PeriodStat> GetDailyCompletionAsync(DateTime fromInclusive, DateTime toExclusive)
        {
            var from = fromInclusive.Date;
            var to = CutOff(toExclusive);

            if (to <= from)
            {
                return new PeriodStat(0, 0);
            }

            //做过的那些天一次查回来——这区间最多几百天，不值得逐日打库
            var doneDates = await _dailyRepository.Queryable()
                .Where(it => it.Date >= from && it.Date < to && it.IsCompleted)
                .Select(it => it.Date)
                .ToListAsync()
                .ConfigureAwait(false);

            var done = new HashSet<DateTime>(doneDates.Select(it => it.Date));

            var (_, skippedWeeks, _, skippedMonths) = await GetPeriodKeysAsync().ConfigureAwait(false);

            int total = 0;
            int skipped = 0;

            for (var day = from; day < to; day = day.AddDays(1))
            {
                //做过的照样是分母里的一条（分子里也有它），不然被跳过的月里做过的那几天会让完成度虚高
                if (done.Contains(day))
                {
                    total++;
                    continue;
                }

                //被跳过的周 / 月里、又确实没写的日子不算欠
                if (IsCoveredBySkip(day, skippedWeeks, skippedMonths))
                {
                    skipped++;
                    continue;
                }

                total++;
            }

            return new PeriodStat(done.Count, total, skipped);
        }

        public Task<PeriodStat> GetWeeklyCompletionAsync(DateTime fromInclusive, DateTime toExclusive)
        {
            return CompletionAsync(fromInclusive, toExclusive, _reviewScheduleService.GetWeek, isWeekly: true);
        }

        public Task<PeriodStat> GetMonthlyCompletionAsync(DateTime fromInclusive, DateTime toExclusive)
        {
            return CompletionAsync(fromInclusive, toExclusive, _reviewScheduleService.GetMonth, isWeekly: false);
        }

        /// <summary>
        /// 两种节拍的完成度是同一段循环：从区间起点按周期往后走，只数<b>已经走完</b>的周期，
        /// 再看它有没有被立过旗。区别只有「怎么取周期」和「旗存在哪张表」。
        /// </summary>
        private async Task<PeriodStat> CompletionAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            Func<DateTime, (DateTime Start, DateTime End, int Year, int Sub)> period,
            bool isWeekly)
        {
            var to = CutOff(toExclusive);
            var today = DateTime.Today;
            var (finalizedWeeks, skippedWeeks, finalizedMonths, skippedMonths) =
                await GetPeriodKeysAsync().ConfigureAwait(false);

            var finalized = isWeekly ? finalizedWeeks : finalizedMonths;
            var skippedKeys = isWeekly ? skippedWeeks : skippedMonths;

            int done = 0;
            int total = 0;
            int skipped = 0;
            var cursor = period(fromInclusive).Start;

            while (true)
            {
                var (start, end, year, sub) = period(cursor);

                //这一个周期还没走完（宽容期还没到）就不算欠——分母只放已经盖棺定论的周期
                if (start >= to || end >= today)
                {
                    break;
                }

                bool isDone = finalized.Contains(MakeKey(year, sub));

                //级联：跳过的月把它里面的周也带上（月是比周更粗的声明）。
                //反向不成立——跳过的周不该盖住整个月。
                bool isSkipped = skippedKeys.Contains(MakeKey(year, sub))
                    || (isWeekly && skippedMonths.Contains(MakeKey(start.Year, start.Month)));

                if (isDone)
                {
                    //做过的照常计入分母，分子里也有它
                    done++;
                    total++;
                }
                else if (isSkipped)
                {
                    skipped++;
                }
                else
                {
                    total++;
                }

                cursor = end.AddDays(1);
            }

            return new PeriodStat(done, total, skipped);
        }

        public async Task<List<RankedItem>> GetFailureRankingAsync(
            DateTime fromInclusive,
            DateTime toExclusive,
            int top = 5)
        {
            var (_, _, fromYear, fromWeek) = _reviewScheduleService.GetWeek(fromInclusive);
            //上界取区间里的**最后一天**，不是「右开端点」：今天是周日时 today+1 会落到下一周，
            //那样就把下周的失败行也算进这一段了。
            var (_, _, toYear, toWeek) = _reviewScheduleService.GetWeek(toExclusive.AddDays(-1));
            int fromKey = MakeKey(fromYear, fromWeek);
            int toKey = MakeKey(toYear, toWeek);

            //Year * 100 + WeekOfYear 单调递增（周次最多 53，不会进位到年份），
            //所以跨年的区间也能用一个 BETWEEN 表达，不用按年拆成几段。
            var rows = await _stumblingBlockRepository.Queryable()
                .Where(it => it.Year * 100 + it.WeekOfYear >= fromKey
                    && it.Year * 100 + it.WeekOfYear <= toKey
                    && it.Description != null
                    && it.Description != "")
                .GroupBy(it => it.Description)
                .Select(it => new
                {
                    Description = it.Description,
                    Count = SqlFunc.AggregateCount(it.Id),
                })
                .ToListAsync()
                .ConfigureAwait(false);

            //排序与截断放在 C# 侧：SqlSugar 的 GroupBy 之后再 OrderBy 别名容易翻出意外的 SQL，
            //而去重后的描述数量是一个人自己的失败条目级别，不是全表规模。
            return [.. rows
                .OrderByDescending(it => it.Count)
                .ThenBy(it => it.Description, StringComparer.CurrentCulture)
                .Take(top)
                .Select(it => new RankedItem(it.Description!, it.Count))];
        }

        /// <summary>
        /// 周课与月课两张表里「已完成」和「已跳过」的周期键，各查一次就够。
        /// 只有立过旗或标过跳过的行才需要，所以 WHERE 把其余行挡在库那边。
        /// </summary>
        private async Task<(
            HashSet<int> FinalizedWeeks,
            HashSet<int> SkippedWeeks,
            HashSet<int> FinalizedMonths,
            HashSet<int> SkippedMonths)> GetPeriodKeysAsync()
        {
            //IsSkipped 是可空列（补列补出来的老行是 NULL），所以判定写 == true
            var weeks = await _weeklyRepository.Queryable()
                .Where(it => it.IsFinalized || it.IsSkipped == true)
                .Select(it => new { it.Year, it.WeekOfYear, it.IsFinalized, it.IsSkipped })
                .ToListAsync()
                .ConfigureAwait(false);

            var months = await _monthlyRepository.Queryable()
                .Where(it => it.IsFinalized || it.IsSkipped == true)
                .Select(it => new { it.Year, it.Month, it.IsFinalized, it.IsSkipped })
                .ToListAsync()
                .ConfigureAwait(false);

            return (
                [.. weeks.Where(it => it.IsFinalized).Select(it => MakeKey(it.Year, it.WeekOfYear))],
                [.. weeks.Where(it => it.IsSkipped == true).Select(it => MakeKey(it.Year, it.WeekOfYear))],
                [.. months.Where(it => it.IsFinalized).Select(it => MakeKey(it.Year, it.Month))],
                [.. months.Where(it => it.IsSkipped == true).Select(it => MakeKey(it.Year, it.Month))]);
        }

        /// <summary>这一天是不是落在被跳过的周或月里（日课完成度的级联扣除）。</summary>
        private bool IsCoveredBySkip(DateTime day, HashSet<int> skippedWeeks, HashSet<int> skippedMonths)
        {
            var (_, _, year, weekOfYear) = _reviewScheduleService.GetWeek(day);

            return skippedWeeks.Contains(MakeKey(year, weekOfYear))
                || skippedMonths.Contains(MakeKey(day.Year, day.Month));
        }

        /// <summary>把「年 + 周次（或月）」压成一个单调可比的整数，用来跨年比大小。</summary>
        private static int MakeKey(int year, int sub)
        {
            return year * 100 + sub;
        }

        /// <summary>
        /// 分母只算已经过去的周期。今天（以及还在宽容期里的本周 / 本月）还没做，不算欠。
        /// </summary>
        private static DateTime CutOff(DateTime toExclusive)
        {
            var today = DateTime.Today;
            return toExclusive > today ? today : toExclusive;
        }
    }
}
