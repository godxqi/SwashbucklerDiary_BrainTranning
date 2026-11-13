using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;
using System.Globalization;

namespace SwashbucklerDiary.Rcl.Services
{
    public class ReviewScheduleService : IReviewScheduleService
    {
        private readonly ISettingService _settingService;

        private readonly IReviewRepository<DailyReviewModel> _dailyReviewRepository;

        private readonly IReviewRepository<WeeklyReviewModel> _weeklyReviewRepository;

        private readonly IReviewRepository<MonthlyReviewModel> _monthlyReviewRepository;

        public ReviewScheduleService(ISettingService settingService,
            IReviewRepository<DailyReviewModel> dailyReviewRepository,
            IReviewRepository<WeeklyReviewModel> weeklyReviewRepository,
            IReviewRepository<MonthlyReviewModel> monthlyReviewRepository)
        {
            _settingService = settingService;
            _dailyReviewRepository = dailyReviewRepository;
            _weeklyReviewRepository = weeklyReviewRepository;
            _monthlyReviewRepository = monthlyReviewRepository;
        }

        /// <summary>日期一律归一化到零点，否则「每天一条」会被时分秒拆散。</summary>
        public static DateTime NormalizeDate(DateTime date) => date.Date;

        public (DateTime Start, DateTime End, int Year, int WeekOfYear) GetWeek(DateTime date)
        {
            var day = NormalizeDate(date);
            // Monday = 0。ISO 周正好是「周五锚点 + 周日宽容期」需要的区间。
            int offset = ((int)day.DayOfWeek + 6) % 7;
            var start = day.AddDays(-offset);
            var end = start.AddDays(6);
            return (start, end, ISOWeek.GetYear(start), ISOWeek.GetWeekOfYear(start));
        }

        public (DateTime Start, DateTime End, int Year, int Month) GetMonth(DateTime date)
        {
            var day = NormalizeDate(date);
            var start = new DateTime(day.Year, day.Month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            return (start, end, day.Year, day.Month);
        }

        private async Task<ReviewTask> GetDailyTaskAsync(DateTime date)
        {
            var day = NormalizeDate(date);
            var model = await _dailyReviewRepository.Queryable()
                .Where(it => it.Date == day)
                .FirstAsync()
                .ConfigureAwait(false);

            bool done = model is not null && model.IsCompleted;
            return new ReviewTask(ReviewKind.Daily, day, day,
                done ? ReviewTaskState.Done : ReviewTaskState.Pending);
        }

        private async Task<ReviewTask> GetWeeklyTaskAsync(DateTime today)
        {
            var day = NormalizeDate(today);
            var (start, end, year, weekOfYear) = GetWeek(day);

            int anchorDay = ClampAnchorDay(_settingService.Get(it => it.WeeklyReviewAnchorDay, (int)DayOfWeek.Friday));
            var anchor = start.AddDays(anchorDay);
            if (day < anchor)
            {
                //本周的锚点还没到，那就是上一周还没结算
                (start, end, year, weekOfYear) = GetWeek(start.AddDays(-7));
                anchor = start.AddDays(anchorDay);
            }

            var graceEnd = anchor.AddDays(Math.Max(0, _settingService.Get(it => it.WeeklyReviewGraceDays, 2)));

            bool finalized = await _weeklyReviewRepository.Queryable()
                .Where(it => it.Year == year && it.WeekOfYear == weekOfYear && it.IsFinalized)
                .AnyAsync()
                .ConfigureAwait(false);

            return new ReviewTask(ReviewKind.Weekly, start, graceEnd, ResolveState(day, graceEnd, finalized));
        }

        private async Task<ReviewTask> GetMonthlyTaskAsync(DateTime today)
        {
            var day = NormalizeDate(today);
            var (start, end, year, month) = GetMonth(day);

            if (day < end)
            {
                //本月最后一天还没到，那就是上一月还没结算
                (start, end, year, month) = GetMonth(start.AddDays(-1));
            }

            var anchor = end;
            var graceEnd = anchor.AddDays(Math.Max(0, _settingService.Get(it => it.MonthlyReviewGraceDays, 3)));

            bool finalized = await _monthlyReviewRepository.Queryable()
                .Where(it => it.Year == year && it.Month == month && it.IsFinalized)
                .AnyAsync()
                .ConfigureAwait(false);

            return new ReviewTask(ReviewKind.Monthly, start, graceEnd, ResolveState(day, graceEnd, finalized));
        }

        public async Task<List<ReviewTask>> GetTodayTasksAsync(DateTime today)
        {
            return
            [
                await GetDailyTaskAsync(today).ConfigureAwait(false),
                await GetWeeklyTaskAsync(today).ConfigureAwait(false),
                await GetMonthlyTaskAsync(today).ConfigureAwait(false),
            ];
        }

        /// <summary>
        /// 把 DayOfWeek（周日 = 0 起算）换算成「距周一多少天」。
        /// 周起点是周一，直接用 DayOfWeek 的数值会整体偏一天（周五会跑到周六去）。
        /// </summary>
        private static int ClampAnchorDay(int dayOfWeek)
        {
            int value = Math.Clamp(dayOfWeek, (int)DayOfWeek.Sunday, (int)DayOfWeek.Saturday);
            return (value + 6) % 7;
        }

        private static ReviewTaskState ResolveState(DateTime today, DateTime graceEnd, bool done)
        {
            if (done)
            {
                return ReviewTaskState.Done;
            }

            return today <= graceEnd ? ReviewTaskState.Pending : ReviewTaskState.Overdue;
        }
    }
}
