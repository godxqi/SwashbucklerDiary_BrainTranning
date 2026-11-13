using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 本周日课总览（只读）。从周课 2.4 进入，按日课章节 1.1 ~ 1.7 分组，
    /// 把这一周每天的原文摊在同一页上，省得在日课页一天天翻。
    /// <para>
    /// 数据只在 <see cref="IDailyReviewService.GetWeekOverviewAsync"/> 里取：
    /// 散文从日记正文解析，情绪与肯定短语从结构化字段取。页面本身不碰 Repository。
    /// </para>
    /// </summary>
    public partial class DailyReviewOverviewPage : ImportantComponentBase
    {
        private List<DailyReviewDayOverview> days = [];

        private string[] dailyTitles = [];

        private string[] microFailureQuestions = [];

        private DateTime weekStart;

        private DateTime weekEnd;

        /// <summary>已经落地过的日期（来自 <c>?Date=</c>）。页面被缓存时，参数没变就不重载。</summary>
        private DateOnly? appliedDate;

        private List<DailyReviewDayOverview> LifeEntries { get; set; } = [];

        private List<DailyReviewDayOverview> StudyEntries { get; set; } = [];

        private List<DailyReviewDayOverview> PositiveEntries { get; set; } = [];

        private int PositiveCount { get; set; }

        private List<DailyReviewDayOverview> FailureEntries { get; set; } = [];

        private int FailureCount { get; set; }

        private List<DailyReviewDayOverview> EnergyEntries { get; set; } = [];

        private List<DailyReviewDayOverview> MoodEntries { get; set; } = [];

        private List<DailyReviewDayOverview> AffirmationEntries { get; set; } = [];

        private int AffirmationCount { get; set; }

        private bool HasAny { get; set; }

        [Inject]
        private IDailyReviewService DailyReviewService { get; set; } = default!;

        /// <summary>周课 2.4 带过来的那一周。缺省是今天所在的那一周。</summary>
        [SupplyParameterFromQuery]
        private DateOnly? Date { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            var target = Date ?? DateOnly.FromDateTime(DateTime.Now);

            if (appliedDate != target)
            {
                appliedDate = target;
                await LoadAsync(target);
            }

            await base.OnParametersSetAsync();
        }

        protected override async Task OnResume()
        {
            //从别处回来时重读一次：这页只读，重读没有丢失改动的风险
            if (appliedDate is DateOnly date)
            {
                await LoadAsync(date);
            }

            await base.OnResume();
        }

        private async Task LoadAsync(DateOnly date)
        {
            dailyTitles = ReviewSectionText.DailyTitles(I18n);
            microFailureQuestions = ReviewSectionText.MicroFailureQuestions(I18n);

            var overview = await DailyReviewService.GetWeekOverviewAsync(date.ToDateTime(TimeOnly.MinValue));

            days = [.. overview.Days];
            weekStart = overview.WeekStart;
            weekEnd = overview.WeekEnd;

            //分组与小计在加载时算一次，渲染时直接用——页面一渲染会多次引用这些列表
            LifeEntries = [.. days.Where(it => !string.IsNullOrWhiteSpace(it.Sections.Life))];
            StudyEntries = [.. days.Where(it => !string.IsNullOrWhiteSpace(it.Sections.Study))];

            PositiveEntries = [.. days.Where(it =>
                it.Sections.ThreePositives.Any(pos => !string.IsNullOrWhiteSpace(pos)))];
            PositiveCount = PositiveEntries.Sum(it =>
                it.Sections.ThreePositives.Count(pos => !string.IsNullOrWhiteSpace(pos)));

            FailureEntries = [.. days.Where(it =>
                it.Sections.MicroFailures.Any(group => group.Any(answer => !string.IsNullOrWhiteSpace(answer))))];
            FailureCount = FailureEntries.Sum(it =>
                it.Sections.MicroFailures.Count(group => group.Any(answer => !string.IsNullOrWhiteSpace(answer))));

            EnergyEntries = [.. days.Where(it => !string.IsNullOrWhiteSpace(it.Sections.EnergyNote))];

            //1.6 只要有情绪芯片或那句原因，就算这一天有内容
            MoodEntries = [.. days.Where(it =>
                it.Mood is not null || !string.IsNullOrWhiteSpace(it.Sections.MoodNote))];

            AffirmationEntries = [.. days.Where(it => it.Affirmations.Count > 0)];
            AffirmationCount = AffirmationEntries.Sum(it => it.Affirmations.Count);

            HasAny = LifeEntries.Count > 0
                || StudyEntries.Count > 0
                || PositiveEntries.Count > 0
                || FailureEntries.Count > 0
                || EnergyEntries.Count > 0
                || MoodEntries.Count > 0
                || AffirmationEntries.Count > 0;

            await InvokeAsync(StateHasChanged);
        }

        private string WeekRange => $"{weekStart:yyyy/MM/dd} - {weekEnd:MM/dd}";

        private string DayLabel(DailyReviewDayOverview day)
            => $"{day.Date:MM/dd} {I18n.T(day.Date.DayOfWeek.ToString())}";

        private string CountLabel(int count, string unit) => $"{count} {I18n.T(unit)}";
    }
}
