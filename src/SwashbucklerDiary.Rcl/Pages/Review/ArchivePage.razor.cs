using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 归档区 · 四个 List（方法文档 7）。文档开头就把它的性质说清楚了：
    /// <b>四个 List 不是待办，是归档区</b>——周课 2.4 的出口问题、以及各来源的自动归档，往这里写。
    /// <para>
    /// 四个 List 的入口各不相同，所以每个 tab 里除了清单还有它自己的「自动入口」：
    /// 7.1 来自月课 3.1 的产出，7.2 来自跨周还没做的周目标，
    /// <b>7.4</b> 来自周课 2.2 的判定（环境原因 / 落选），而且它是唯一带<b>回流计数</b>的一张——
    /// 同一条在这里再出现两次（累计 3 次）说明它不是偶发，就该回到 2.2 重新进候选池。
    /// </para>
    /// </summary>
    public partial class ArchivePage : ImportantComponentBase
    {
        /// <summary>文档 7.4 的出口：同一个条目累计 3 次就回流。</summary>
        private const int FeedbackThreshold = 3;

        private static readonly ArchiveKind[] kinds = Enum.GetValues<ArchiveKind>();

        private ArchiveKind kind = ArchiveKind.SelfInsight;

        private List<ArchiveItemModel> items = [];

        /// <summary>7.4 里攒够次数、还没回流的条目。</summary>
        private List<ArchiveItemModel> feedbackCandidates = [];

        /// <summary>7.2 的自动入口：跨周还没做的周目标。</summary>
        private List<WeeklyGoalModel> carriedOverGoals = [];

        private bool showAdd;

        private bool showDelete;

        private ArchiveItemModel? pendingDelete;

        private bool showFeedback;

        private ArchiveItemModel? pendingFeedback;

        [Inject]
        private IArchiveService ArchiveService { get; set; } = default!;

        [Inject]
        private IWeeklyReviewService WeeklyReviewService { get; set; } = default!;

        /// <summary>从复盘主页/库表进来时指定看哪一个 List。缺省是 7.1。</summary>
        [SupplyParameterFromQuery]
        private int? Kind { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (Kind is int value && Enum.IsDefined((ArchiveKind)value))
            {
                kind = (ArchiveKind)value;
            }

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
            items = await ArchiveService.QueryAsync(kind);

            //回流候选只在 7.4 有意义；自动入口只在 7.2 有意义
            feedbackCandidates = kind == ArchiveKind.MicroFailure
                ? await ArchiveService.GetFeedbackCandidatesAsync(FeedbackThreshold)
                : [];

            carriedOverGoals = kind == ArchiveKind.FuturePlan
                ? await WeeklyReviewService.GetCarriedOverGoalsAsync(DateTime.Now)
                : [];

            await InvokeAsync(StateHasChanged);
        }

        private async Task SwitchKindAsync(ArchiveKind target)
        {
            if (kind == target)
            {
                return;
            }

            kind = target;

            //与 VirtuePage 同理：切到 7.4 之后离开再进，不该跳回 7.1
            To($"archive?kind={(int)kind}", replace: true);

            await LoadAsync();
        }

        // ———————————————— 手动补充 ————————————————

        private async Task AddAsync(string content)
        {
            showAdd = false;

            //归档区的去重与计数收口在服务里：同一条内容再写一次是「又出现了一次」
            if (!await ArchiveService.ArchiveAsync(kind, ArchiveSource.Manual, content))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            await LoadAsync();
        }

        // ———————————————— 7.2 的自动入口 ————————————————

        /// <summary>把「跨周还没做」的周目标归档成 7.2 的一条新计划（文档 7.2 的入口）。</summary>
        private async Task ArchiveGoalAsync(WeeklyGoalModel goal)
        {
            if (!await ArchiveService.ArchiveAsync(ArchiveKind.FuturePlan,
                    ArchiveSource.WeeklyGoal, goal.Content, goal.WeeklyReviewId))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            await LoadAsync();
        }

        // ———————————————— 7.4 的回流 ————————————————

        private void AskFeedback(ArchiveItemModel item)
        {
            pendingFeedback = item;
            showFeedback = true;
        }

        /// <summary>
        /// 回流：标记已回流，并在本周建一条判为「未定」的微观失败——它就回到周课 2.1 的清单里等人归类。
        /// </summary>
        private async Task ConfirmFeedbackAsync()
        {
            showFeedback = false;

            var target = pendingFeedback;
            pendingFeedback = null;
            if (target is null)
            {
                return;
            }

            if (!await ArchiveService.FeedBackAsync(target.Id, DateTime.Now))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            await AlertService.SuccessAsync(I18n.T("Fed back into this week's 2.2"));
            await LoadAsync();
        }

        // ———————————————— 删除 ————————————————

        private void AskDelete(ArchiveItemModel item)
        {
            pendingDelete = item;
            showDelete = true;
        }

        private async Task ConfirmDeleteAsync()
        {
            showDelete = false;

            var target = pendingDelete;
            pendingDelete = null;
            if (target is null)
            {
                return;
            }

            if (!await ArchiveService.DeleteAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            items.RemoveAll(it => it.Id == target.Id);

            await InvokeAsync(StateHasChanged);
        }

        // ———————————————— 展示 ————————————————

        //名字的映射收在 ReviewSectionText 里，周课 2.4 的归档面板也用它——两处各抄一份迟早走样
        private string KindName(ArchiveKind value) => ReviewSectionText.ArchiveKindText(I18n, value);

        /// <summary>每个 List 的入口标注，直接取自文档 7 各小节。</summary>
        private string KindDescription => I18n.T(kind switch
        {
            ArchiveKind.FuturePlan => "Where the weekly goals that carried over past their week end up.",
            ArchiveKind.DailyThought => "Thoughts that keep coming back in the daily review but never became goals. Added by hand.",
            ArchiveKind.MicroFailure => "Reached from 2.2 (the stumbling block that lost this week's single slot). When the same entry shows up three times, it is not a one-off.",
            _ => "Where the three-column review in 3.1 ends up, plus the patterns behind repeated triggers.",
        });

        private string SourceName(ArchiveSource source) => I18n.T(source switch
        {
            ArchiveSource.EnvironmentFailure => "From 2.1: the environment, not me",
            ArchiveSource.RejectedBlock => "From 2.2: rejected this week",
            ArchiveSource.SelfInsight => "From the monthly review 3.1",
            ArchiveSource.WeeklyGoal => "From a weekly goal that carried over",
            _ => "Added by hand",
        });
    }
}
