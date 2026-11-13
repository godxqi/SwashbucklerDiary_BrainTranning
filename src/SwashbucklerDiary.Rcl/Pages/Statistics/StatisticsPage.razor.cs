using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 统计页（需求 16）：节拍完成度 + 高频模式（失败排行 / 触因排行 / 成就增长）。
    /// <para>
    /// 全部数字都来自 SQL 聚合，页面只负责画条形；四个卡片说的都是「同一个量在不同条目上的大小」，
    /// 属于单序列的量值对比，所以只用一条色相，也不用图例。
    /// </para>
    /// </summary>
    public partial class StatisticsPage : ImportantComponentBase
    {
        /// <summary>时间段芯片。只作用于失败排行——完成度按「已经走完的周期」算，跟着它走没意义。</summary>
        private static readonly (string Key, int Days)[] periods =
        [
            ("Last 30 days", 30),
            ("Last 90 days", 90),
            ("Last year", 365),
        ];

        private const int topCount = 5;

        private const int achievementMonths = 6;

        /// <summary>
        /// 芯片的 key。预先算成静态数组：它是属性的话，razor 里那两处访问
        /// （长度 + 取 key）每次渲染都要重新 LINQ 一遍、新分配一个数组。
        /// </summary>
        private static readonly string[] periodKeys = [.. periods.Select(it => it.Key)];

        private int periodIndex = 1;

        private PeriodStat dailyStat = new(0, 0);

        private PeriodStat weeklyStat = new(0, 0);

        private PeriodStat monthlyStat = new(0, 0);

        private List<RankedItem> failures = [];

        private List<TriggerCount> triggers = [];

        private List<AchievementGrowth> achievements = [];

        private int achievementTotal;

        private int achievementThisMonth;

        [Inject]
        private IReviewStatsService ReviewStatsService { get; set; } = default!;

        [Inject]
        private ITriggerService TriggerService { get; set; } = default!;

        [Inject]
        private IVirtueService VirtueService { get; set; } = default!;

        private int MaxAchievement => achievements.Count == 0 ? 0 : achievements.Max(it => it.Count);

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();

            await base.OnInitializedAsync();
        }

        protected override async Task OnResume()
        {
            //在别处记了肯定 / 判了失败行之后回到统计页，数字要跟着变
            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            var today = DateTime.Today;
            var from = today.AddDays(-periods[periodIndex].Days);
            var to = today.AddDays(1);

            dailyStat = await ReviewStatsService.GetDailyCompletionAsync(from, to);
            weeklyStat = await ReviewStatsService.GetWeeklyCompletionAsync(from, to);
            monthlyStat = await ReviewStatsService.GetMonthlyCompletionAsync(from, to);
            failures = await ReviewStatsService.GetFailureRankingAsync(from, to, topCount);

            //GetWithCountsAsync 已经按次数从多到少排好，这里只要把一次都没记过的摘掉
            triggers = [.. (await TriggerService.GetWithCountsAsync())
                .Where(it => it.Count > 0)
                .Take(topCount)];

            var growth = await VirtueService.GetAchievementGrowthAsync();
            achievementTotal = growth.Sum(it => it.Count);
            achievementThisMonth = growth
                .Where(it => it.Year == today.Year && it.Month == today.Month)
                .Sum(it => it.Count);
            achievements = [.. growth.TakeLast(achievementMonths)];

            await InvokeAsync(StateHasChanged);
        }

        private async Task SelectPeriodAsync(int index)
        {
            if (periodIndex == index)
            {
                return;
            }

            periodIndex = index;
            await LoadAsync();
        }

        /// <summary>
        /// 分母是 0 表示这段时间里压根没有该做的节拍（刚装上、或者时间段还没走完），
        /// 这时显示「—」而不是 0%——「没数据」和「一次都没做」是两回事。
        /// <para>
        /// 有「跳过」的时候把条数缀在后面：分母是扣掉它们之后算出来的，
        /// 不说明一下，补足时看到数字变大变小会摸不着头脑。
        /// </para>
        /// </summary>
        private string FormatStat(PeriodStat stat)
        {
            string baseText = stat.HasData
                ? $"{stat.Done}/{stat.Total} · {stat.Percent}%"
                : "—";

            return stat.Skipped > 0
                ? $"{baseText} · {I18n.T("Skipped")} {stat.Skipped}"
                : baseText;
        }

        /// <summary>条形的宽度：对着这一组里最大的那条算，最大的那条占满。</summary>
        private static double PercentOf(int value, int max)
        {
            if (max <= 0)
            {
                return 0;
            }

            return Math.Clamp(value * 100.0 / max, 0, 100);
        }
    }
}
