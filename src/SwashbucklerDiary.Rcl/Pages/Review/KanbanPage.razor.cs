using Masa.Blazor;
using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 复盘主页 —— 打开 App 的第一眼（需求 1）。
    /// 「今天该做什么」完全由 <see cref="IReviewScheduleService"/> 判定，页面自己不算节拍。
    /// 另外两张提醒卡是「回环自动化」露头的地方：触因攒够 3 次（需求 12 / 文档 5.2）、
    /// 7.4 里的条目累计 3 次（文档 7.4 的出口）。
    /// </summary>
    public partial class KanbanPage : ImportantComponentBase
    {
        /// <summary>文档 5.2：攒 3 个以上同类触因就交给月课 3.1。</summary>
        private const int TriggerThreshold = 3;

        /// <summary>文档 7.4：同一条归档再出现两次（累计 3 次）说明它不是偶发。</summary>
        private const int FeedbackThreshold = 3;

        private StringNumber tab = 0;

        private bool affirmationBarVisible;

        private int affirmationBarCount;

        private List<ReviewTask> tasks = [];

        private List<SelfAffirmationModel> affirmations = [];

        private List<TriggerCount> readyTriggers = [];

        /// <summary>7.4 里攒够次数、还没回流的条目。</summary>
        private List<ArchiveItemModel> feedbackCandidates = [];

        private SwiperTabItems swiperTabItems = default!;

        [Inject]
        private IReviewScheduleService ReviewScheduleService { get; set; } = default!;

        [Inject]
        private IAffirmationService AffirmationService { get; set; } = default!;

        [Inject]
        private ITriggerService TriggerService { get; set; } = default!;

        [Inject]
        private IArchiveService ArchiveService { get; set; } = default!;

        protected override void ReadSettings()
        {
            base.ReadSettings();

            tab = SettingService.Get(it => it.KanbanDefaultTab);
            affirmationBarVisible = SettingService.Get(it => it.AffirmationBarVisible);
            affirmationBarCount = SettingService.Get(it => it.AffirmationBarCount);
        }

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();

            await base.OnInitializedAsync();
        }

        protected override async Task OnResume()
        {
            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            tasks = await ReviewScheduleService.GetTodayTasksAsync(DateTime.Now);
            affirmations = affirmationBarVisible
                ? await AffirmationService.GetActiveAsync(affirmationBarCount)
                : [];
            readyTriggers = await TriggerService.GetReadyForMonthlyAsync(TriggerThreshold);
            feedbackCandidates = await ArchiveService.GetFeedbackCandidatesAsync(FeedbackThreshold);
        }

        /// <summary>点一下就重复一次并计数（需求 10）。计数直接回写屏幕上那个数字，不用重查。</summary>
        private async Task RepeatAffirmationAsync(SelfAffirmationModel affirmation)
        {
            var updated = await AffirmationService.RepeatAsync(affirmation.Id);
            if (updated is null)
            {
                return;
            }

            affirmation.RepeatCount = updated.RepeatCount;

            await InvokeAsync(StateHasChanged);
        }

        private void OpenTask(ReviewTask task)
        {
            switch (task.Kind)
            {
                case ReviewKind.Daily:
                    To($"dailyReview?Date={task.PeriodStart:yyyy-MM-dd}");
                    return;
                case ReviewKind.Weekly:
                    To($"weeklyReview?Date={task.PeriodStart:yyyy-MM-dd}");
                    return;
                default:
                    To($"monthlyReview?Date={task.PeriodStart:yyyy-MM-dd}");
                    return;
            }
        }

        private void OpenTriggers()
        {
            To("triggers");
        }

        /// <summary>7.4 的回流提醒：点进去落在归档区的「经历的微小失败」那一栏上。</summary>
        private void OpenArchiveMicroFailures()
        {
            To($"archive?kind={(int)ArchiveKind.MicroFailure}");
        }

        private static string TaskTitleKey(ReviewKind kind) => kind switch
        {
            ReviewKind.Daily => "Daily review",
            ReviewKind.Weekly => "Weekly review",
            _ => "Monthly review",
        };

        private static string TaskIcon(ReviewTask task)
        {
            if (task.State == ReviewTaskState.Done)
            {
                return "check_circle";
            }

            return task.State == ReviewTaskState.Overdue ? "error_outline" : "radio_button_unchecked";
        }

        private string TaskSubtitle(ReviewTask task)
        {
            return task.State switch
            {
                ReviewTaskState.Done => I18n.T("Completed"),
                ReviewTaskState.Overdue => I18n.T("Overdue"),
                _ => task.Kind == ReviewKind.Daily
                                    ? task.PeriodStart.ToString("yyyy/MM/dd")
                                    : $"{task.PeriodStart:yyyy/MM/dd} - {task.GraceEndDate:MM/dd}",
            };
        }
    }
}
