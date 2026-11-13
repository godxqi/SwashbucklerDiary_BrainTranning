using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Models;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 周课（方法文档 2）。文档说「这一节是全系统的心脏」，所以严格按 2.1 ~ 2.6 的章节顺序排，
    /// 一节不缺：
    /// <list type="number">
    /// <item>2.1 本周三个目标结算 —— 达成进成就列表，未达成逐项归因</item>
    /// <item>2.2 数频率 —— 列出这一周的微观失败，<b>人</b>归类、<b>人</b>判性（机器只数次数）</item>
    /// <item>2.3 提炼 —— 从候选池里最多升一条</item>
    /// <item>2.4 四个记录表检查 —— 四道出口问题，外加直接归档到四个 List 的小面板</item>
    /// <item>2.5 本周一件新事物</item>
    /// <item>2.6 下周三个目标</item>
    /// </list>
    /// <para>
    /// 点选立刻落库（判定 / 归类 / 目标），只有散文（2.1 追问、2.4、2.5）走「脏了就存」，
    /// 所以在返回键上挂了一个保存动作。
    /// </para>
    /// </summary>
    public partial class WeeklyReviewPage : ImportantComponentBase
    {
        private static readonly BlockVerdict[] verdicts = Enum.GetValues<BlockVerdict>();

        private DateTime selectedDate = DateTime.Now.Date;

        /// <summary>
        /// 已经落地过的那个日期（来自 <c>?Date=</c>）。用它判断参数到底变没变——
        /// 父组件重渲染也会走 <c>OnParametersSetAsync</c>，不挡一道就会反复重载。
        /// </summary>
        private DateOnly? appliedDate;

        private bool showDatePicker;

        private bool showMenu;

        /// <summary>这一周一条日课都没写。2.1 的清单和候选池都是从日课长出来的，先提醒一句。</summary>
        private bool blankWeek;

        /// <summary>写过日课的日子。桌面端日历标点用；手机端标不了点（PMobileDatePicker 没有 Events）。</summary>
        private DateOnly[] eventsDates = [];

        private List<DynamicListItem> menuItems = [];

        /// <summary>日历的可选上界：补的是过去，写不了未来。</summary>
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        /// <summary>日历上显示的那个值。只读——换日期一律走 <see cref="SwitchToDateAsync"/>。</summary>
        private DateOnly SelectedDate => DateOnly.FromDateTime(selectedDate);

        /// <summary>⋮ 里那一项该写「跳过」还是「撤销跳过」。</summary>
        private string SkipLabelKey => review?.IsSkipped == true ? "Unskip this week" : "Skip this week";

        private int year;

        private int weekOfYear;

        private DateTime weekStart;

        private DateTime weekEnd;

        //下一周（2.6「下周三个目标」挂到它身上）与它自己的行
        private int nextYear;

        private int nextWeekOfYear;

        private DateTime nextWeekStart;

        private DateTime nextWeekEnd;

        /// <summary>null = 这一周还没开过周课。只有真的要落东西时才建行。</summary>
        private WeeklyReviewModel? review;

        private List<StumblingBlockView> blocks = [];

        private List<string> knownCategories = [];

        /// <summary>
        /// 7.4 里累计 ≥3 次的条目内容。文档 7.4 的出口说这类条目「回到 2.1 重新进入候选池」，
        /// 所以它也要算一条进池的依据——否则回流回来的那条在 2.1 看得见、却永远进不了池。
        /// 回流时归档内容被原样写成了 <c>Description</c>，所以按描述能精确对上。
        /// </summary>
        private HashSet<string> recurringContents = new(StringComparer.CurrentCulture);

        /// <summary>这一周自己的三个目标。<b>2.1 结算的就是它</b>（2026-10-06 起结算「本周」而不是「上周」）。</summary>
        private List<WeeklyGoalModel> goals = [];

        /// <summary>下一周的目标。<b>2.6「下周三个目标」编辑的是它</b>，新目标也挂到下一周的周课记录上。</summary>
        private List<WeeklyGoalModel> nextWeekGoals = [];

        private WeeklyReviewSections sections = new();

        private string[] weeklyTitles = [];

        private string[] weeklyDescriptions = [];

        private string[] exitQuestions = [];

        /// <summary>2.4 归档面板可选的四张 List，顺序与归档区一致。</summary>
        private static readonly ArchiveKind[] archiveKinds = Enum.GetValues<ArchiveKind>();

        /// <summary>2.4 归档面板当前选中的 List，默认与归档区「手动添加」一致。</summary>
        private ArchiveKind archiveKind = ArchiveKind.SelfInsight;

        /// <summary>2.4 归档面板里待归档的草稿。它不写进周课正文，也不参与「脏了就存」。</summary>
        private string archiveContent = string.Empty;

        /// <summary>归档进行中，防止连点把同一条重复提交（7.4 的次数会被多刷）。</summary>
        private bool archiveSaving;

        /// <summary>S/M/A/R/T 五问的题干，与目标对话框共用同一份（见 ReviewSectionText）。</summary>
        private string[] smartKeys = [];

        /// <summary>2.2 从候选池里挑中的那一条。</summary>
        private StumblingBlockModel? chosenBlock;

        private bool dirty;

        private bool showGoalDialog;

        private WeeklyGoalModel? editingGoal;

        private bool showNewCategory;

        private bool showDeleteGoal;

        private WeeklyGoalModel? pendingDeleteGoal;

        /// <summary>2.1 里正等着「新建类别」的那条失败。</summary>
        private StumblingBlockModel? pendingCategoryBlock;

        [Inject]
        private IWeeklyReviewService WeeklyReviewService { get; set; } = default!;

        [Inject]
        private IStumblingBlockService StumblingBlockService { get; set; } = default!;

        [Inject]
        private IReviewScheduleService ReviewScheduleService { get; set; } = default!;

        [Inject]
        private IArchiveService ArchiveService { get; set; } = default!;

        [Inject]
        private IDailyReviewService DailyReviewService { get; set; } = default!;

        /// <summary>复盘主页点进来的那一周。缺省就是当前该做的那一周。</summary>
        [SupplyParameterFromQuery]
        private DateOnly? Date { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //用返回键离开时也要落地，否则「写了一半按返回」等于白写
            NavigateController.AddHistoryAction(LeavePageSaveAsync, false);

            menuItems =
            [
                new(this, "Back to today", "today", GoCurrentWeekAsync),
                new(this, "This month's monthly review", "calendar_month", GoMonthlyAsync),
                new(this, () => SkipLabelKey, "block", ToggleSkipAsync),
            ];
        }

        /// <summary>
        /// 日期一律从这里落地，<b>不放在 <c>OnInitializedAsync</c> 里</b>。
        /// 页面是被缓存的：从复盘主页 / ⋮ 菜单带着新的 <c>?Date=</c> 再进来时，
        /// 初始化只跑一次，光靠它读日期会停在上一次那一周。
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            var target = Date ?? DateOnly.FromDateTime(DateTime.Now);

            if (appliedDate != target)
            {
                if (appliedDate is not null)
                {
                    await SaveIfDirtyAsync();
                }

                await ApplyDateAsync(target);
            }

            await base.OnParametersSetAsync();
        }

        /// <summary>切到某一周：换状态、重新读。地址栏由调用方另行同步。</summary>
        private async Task ApplyDateAsync(DateOnly date)
        {
            appliedDate = date;
            selectedDate = date.ToDateTime(TimeOnly.MinValue).Date;

            //归档草稿是这一周的现场输入，换周就作废——免得把上周想归档的东西记到这一周名下
            archiveContent = string.Empty;

            await LoadAsync();
        }

        protected override async Task OnResume()
        {
            await SaveIfDirtyAsync();
            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            weeklyTitles = ReviewSectionText.WeeklyTitles(I18n);
            weeklyDescriptions = ReviewSectionText.WeeklyDescriptions(I18n);
            exitQuestions = ReviewSectionText.ExitQuestions(I18n);
            smartKeys = ReviewSectionText.WeeklySmartKeys(I18n);

            var week = ReviewScheduleService.GetWeek(selectedDate);
            weekStart = week.Start;
            weekEnd = week.End;
            year = week.Year;
            weekOfYear = week.WeekOfYear;

            var next = ReviewScheduleService.GetWeek(weekStart.AddDays(7));
            nextWeekStart = next.Start;
            nextWeekEnd = next.End;
            nextYear = next.Year;
            nextWeekOfYear = next.WeekOfYear;

            review = await WeeklyReviewService.GetAsync(year, weekOfYear);
            sections = await WeeklyReviewService.GetSectionsAsync(year, weekOfYear);
            blocks = await StumblingBlockService.GetByWeekAsync(year, weekOfYear);
            knownCategories = await StumblingBlockService.GetKnownCategoriesAsync();
            recurringContents = new HashSet<string>(
                await ArchiveService.GetRecurringContentsAsync(),
                StringComparer.CurrentCulture);
            goals = await WeeklyReviewService.GetGoalsAsync(year, weekOfYear);
            nextWeekGoals = await WeeklyReviewService.GetGoalsAsync(nextYear, nextWeekOfYear);

            //这一周一条日课都没有时先提醒一句：2.2 的清单和候选池都是从日课长出来的，
            //直接补周课会补出一个空壳。数据顺便留着给日历标点用。
            var reviewDates = await DailyReviewService.GetReviewDatesAsync();
            eventsDates = [.. reviewDates.Select(DateOnly.FromDateTime)];
            blankWeek = !reviewDates.Any(it => it.Date >= weekStart && it.Date <= weekEnd);

            //上一周已经换掉了，候选自然作废
            chosenBlock = null;
            dirty = false;

            await InvokeAsync(StateHasChanged);
        }

        // ———————————————— 2.1 数频率 ————————————————

        /// <summary>按类别汇总这一周的条数。没归类的单列一栏，免得它们凭空消失。</summary>
        private List<(string Category, int Count)> CategorySummary
        {
            get
            {
                var groups = blocks
                    .GroupBy(it => string.IsNullOrWhiteSpace(it.Block.Category)
                        ? I18n.T("Uncategorised")
                        : it.Block.Category!)
                    .Select(it => (Category: it.Key, Count: it.Count()))
                    .OrderByDescending(it => it.Count)
                    .ThenBy(it => it.Category, StringComparer.CurrentCulture);

                return [.. groups];
            }
        }

        /// <summary>
        /// 2.2 的候选池。三条进池的依据都不离「重复才叫绊脚石」这件事：
        /// <list type="bullet">
        /// <item>文档 2.1 的规则：<b>同类出现 ≥2 次</b>（机器数次数，人不需要自己数）</item>
        /// <item>人工判成「绊脚石」的单条（判断以人为准）</item>
        /// <item>文档 7.4 的出口：在 7.4 里<b>累计 ≥3 次</b>的条目回流回来时直接进池</item>
        /// </list>
        /// </summary>
        private List<Candidate> Candidates
        {
            get
            {
                Dictionary<string, int> counts = new(StringComparer.CurrentCulture);
                foreach (var view in blocks)
                {
                    if (!string.IsNullOrWhiteSpace(view.Block.Category))
                    {
                        counts[view.Block.Category!] = counts.GetValueOrDefault(view.Block.Category!) + 1;
                    }
                }

                return [.. blocks
                    .Select(it =>
                    {
                        string? category = string.IsNullOrWhiteSpace(it.Block.Category) ? null : it.Block.Category;
                        bool recurring = recurringContents.Contains(it.Block.Description ?? string.Empty);
                        return new Candidate(it, category, category is null ? 0 : counts[category], recurring);
                    })
                    .Where(it => it.View.Block.Verdict == BlockVerdict.StumblingBlock
                        || it.CategoryCount >= 2
                        || it.Recurring)];
            }
        }

        /// <summary>候选池里的一条：失败本身 + 它那一类的名字与次数 + 是不是 7.4 回流回来的。</summary>
        private sealed record Candidate(StumblingBlockView View,
            string? Category,
            int CategoryCount,
            bool Recurring);

        /// <summary>候选条目的来源：按频次进来的写「类别 ×次数」，其余注明是靠什么进来的。</summary>
        private string CandidateLabel(Candidate candidate)
        {
            string head = candidate.Category is string category
                ? $"{category} ×{candidate.CategoryCount}"
                : I18n.T("Uncategorised");

            if (candidate.CategoryCount >= 2)
            {
                return head;
            }

            return candidate.Recurring
                ? $"{head} · {I18n.T("Reflowed from 7.4")}"
                : $"{head} · {I18n.T("Marked by hand")}";
        }

        private static string CandidateRowClass(bool isOn)
            => isOn ? "review-candidate review-candidate--on" : "review-candidate";

        private void MarkDirty()
        {
            dirty = true;
        }

        private void ToggleChosenBlock(StumblingBlockModel block)
        {
            chosenBlock = chosenBlock?.Id == block.Id ? null : block;
        }

        private async Task SetCategoryAsync(StumblingBlockModel block, string category)
        {
            //再点一下同一个类别＝取消归类
            string? value = string.Equals(block.Category, category, StringComparison.CurrentCulture) ? null : category;

            await ApplyJudgementAsync(block, block.Verdict, value);
        }

        private void AskNewCategory(StumblingBlockModel block)
        {
            pendingCategoryBlock = block;
            showNewCategory = true;
        }

        private async Task AddCategoryAsync(string name)
        {
            showNewCategory = false;

            var block = pendingCategoryBlock;
            pendingCategoryBlock = null;

            string value = name?.Trim() ?? string.Empty;
            if (block is null || value.Length == 0)
            {
                return;
            }

            if (!knownCategories.Contains(value))
            {
                knownCategories.Add(value);
                knownCategories.Sort(StringComparer.CurrentCulture);
            }

            await ApplyJudgementAsync(block, block.Verdict, value);
        }

        /// <summary>判定与归类走同一条路：写库 → 失败提示 → 刷新。</summary>
        private async Task ApplyJudgementAsync(StumblingBlockModel block, BlockVerdict verdict, string? category)
        {
            if (!await StumblingBlockService.JudgeAsync(block, verdict, category))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            await InvokeAsync(StateHasChanged);
        }

        /// <summary>跳去那一天的日课，看看这条失败的四问答案写了什么。</summary>
        private async Task OpenSourceDiaryAsync(StumblingBlockView view)
        {
            if (view.Date is not DateTime date)
            {
                return;
            }

            await SaveIfDirtyAsync();
            To($"dailyReview?Date={date:yyyy-MM-dd}");
        }

        // ———————————————— 2.3 目标卡 ————————————————

        private void OpenGoalDialog(WeeklyGoalModel? goal)
        {
            editingGoal = goal;
            showGoalDialog = true;
        }

        /// <summary>2.3 选中一条 → 带着它去开一张目标卡（挂到<b>下一周</b>，见 2.6）。</summary>
        private void PromoteChosenBlock()
        {
            if (chosenBlock is null)
            {
                return;
            }

            //WeeklyReviewId 留空：SaveGoalAsync 会把它落到下一周那行上。
            //这里不能填 review?.Id —— 那是「这一周」，而提炼出来的目标是下周要做的。
            OpenGoalDialog(new WeeklyGoalModel
            {
                Domain = GoalDomain.PersonalDevelopment,
                Content = chosenBlock.Description,
            });
        }

        private async Task SaveGoalAsync(WeeklyGoalModel goal)
        {
            showGoalDialog = false;

            //2.6 编辑的是「下周三个目标」，所以新目标一律挂到**下一周**那行上；
            //下一周还没开过周课就先建行（只有真要落目标时才建，看一眼对话框又取消不该凭空多一行）。
            if (goal.WeeklyReviewId == Guid.Empty)
            {
                var nextWeekReview = await WeeklyReviewService.GetOrCreateAsync(nextWeekStart);
                goal.WeeklyReviewId = nextWeekReview.Id;
            }

            if (!await WeeklyReviewService.SaveGoalAsync(goal))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            chosenBlock = null;
            nextWeekGoals = await WeeklyReviewService.GetGoalsAsync(nextYear, nextWeekOfYear);

            await InvokeAsync(StateHasChanged);
        }

        private void AskDeleteGoal(WeeklyGoalModel goal)
        {
            pendingDeleteGoal = goal;
            showDeleteGoal = true;
        }

        private async Task ConfirmDeleteGoalAsync()
        {
            showDeleteGoal = false;

            var target = pendingDeleteGoal;
            pendingDeleteGoal = null;
            if (target is null)
            {
                return;
            }

            if (!await WeeklyReviewService.DeleteGoalAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            goals.RemoveAll(it => it.Id == target.Id);

            await InvokeAsync(StateHasChanged);
        }

        // ———————————————— 2.1 结算 ————————————————

        /// <summary>S/M/A/R/T 五项。顺序与 <see cref="WeeklyGoalModel"/> 里那五个字段一致。</summary>
        private const int SmartCount = 5;

        /// <summary>
        /// 结算：<b>完成 / 未达成二选一</b>。达成会自动写进成就列表（6.3），不需要再填什么；
        /// 未达成的归因是逐项写在目标那五个 <c>SmartXxxNote</c> 上的，由保存路径一并落库。
        /// </summary>
        private async Task SettleAsync(WeeklyGoalModel goal, GoalStatus status)
        {
            //结算记在「正在看的这一周」，不是「现在这一周」——补过去的周课时才不会被记串
            if (!await WeeklyReviewService.SettleGoalAsync(goal, selectedDate, status))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            goals = await WeeklyReviewService.GetGoalsAsync(year, weekOfYear);

            await InvokeAsync(StateHasChanged);
        }

        // ——— 未达成的归因：哪几项 SMART 是原因、各自为什么 ———
        // 三种状态：null = 没勾；空串 = 勾了还没写；非空 = 勾了并写了原因。
        // 这样「勾没勾」不必另存标记（库里也是这么认的），而且勾了一半切走时不会丢东西。

        private static string? Issue(int index, WeeklyGoalModel goal) => index switch
        {
            0 => goal.SmartSpecificNote,
            1 => goal.SmartMeasurableNote,
            2 => goal.SmartAchievableNote,
            3 => goal.SmartRelevantNote,
            _ => goal.SmartTimeBoundNote,
        };

        private static void SetIssue(int index, WeeklyGoalModel goal, string? value)
        {
            switch (index)
            {
                case 0: goal.SmartSpecificNote = value; break;
                case 1: goal.SmartMeasurableNote = value; break;
                case 2: goal.SmartAchievableNote = value; break;
                case 3: goal.SmartRelevantNote = value; break;
                default: goal.SmartTimeBoundNote = value; break;
            }
        }

        private static bool IsIssueChecked(WeeklyGoalModel goal, int index) => Issue(index, goal) is not null;

        private static string SmartText(WeeklyGoalModel goal, int index) => index switch
        {
            0 => goal.SmartSpecific,
            1 => goal.SmartMeasurable,
            2 => goal.SmartAchievable,
            3 => goal.SmartRelevant,
            _ => goal.SmartTimeBound,
        } ?? string.Empty;

        /// <summary>那一项的原文；没写就给个破折号——不然那一行会只剩个字母。</summary>
        private static string SmartTextOrDash(WeeklyGoalModel goal, int index)
            => SmartText(goal, index) is { Length: > 0 } text ? text : "—";

        /// <summary>那五项里至少写了一项。一项都没写就别摆一排空框了（没填 SMART 的老目标）。</summary>
        private static bool HasAnySmart(WeeklyGoalModel goal)
            => Enumerable.Range(0, SmartCount).Any(i => !string.IsNullOrWhiteSpace(SmartText(goal, i)));

        private void ToggleIssue(WeeklyGoalModel goal, int index)
        {
            SetIssue(index, goal, IsIssueChecked(goal, index) ? null : string.Empty);
            dirty = true;
        }

        private void SetIssueAndMark(WeeklyGoalModel goal, int index, string? value)
        {
            SetIssue(index, goal, value);
            dirty = true;
        }

        /// <summary>勾了却没写原因的。空串会被当成「没勾」读回来，所以显式拦一下。</summary>
        private static bool HasBlankIssue(WeeklyGoalModel goal)
            => Enumerable.Range(0, SmartCount)
                .Any(i => IsIssueChecked(goal, i) && string.IsNullOrWhiteSpace(Issue(i, goal)));

        /// <summary>
        /// 把未达成目标的归因写回去。整行更新、幂等，不做精细 diff——
        /// 一周最多三个目标，多写两行的代价远小于维护一份脏记录的复杂度。
        /// </summary>
        private async Task PersistDiagnosesAsync()
        {
            foreach (var goal in goals.Where(it => it.Status == GoalStatus.Dropped))
            {
                await WeeklyReviewService.SaveGoalAsync(goal);
            }
        }

        /// <summary>
        /// 右下角保存按钮走这里：**只有主动保存才拦**「勾了没写原因」。
        /// 自动保存（按返回 / 换周 / 回到本页）不拦——那时弹提示没人看得见，
        /// 而拦下来会连当天的散文一起存不上，那是更大的损失。空串如实入库，回来还原成「勾了没写」。
        /// </summary>
        private async Task SaveFromButtonAsync()
        {
            if (goals.Any(HasBlankIssue))
            {
                await AlertService.WarningAsync(I18n.T("Every checked item needs a reason."));
                return;
            }

            await SaveAsync(true);
        }

        // ———————————————— 2.4 归档与日课总览 ————————————————

        /// <summary>2.4 顶部的入口：带着当前周去「本周日课总览」。走之前先把没存的散文落地。</summary>
        private async Task OpenDailyOverviewAsync()
        {
            await SaveIfDirtyAsync();
            To($"dailyReviewOverview?Date={selectedDate:yyyy-MM-dd}");
        }

        /// <summary>
        /// 2.4 底部的小表单：把一段自由文本归档到选中的 List。
        /// 来源记 Manual（与归档区「手动添加」一致），重复内容沿用 ArchiveService 的去重规则。
        /// </summary>
        private async Task ArchiveContentAsync()
        {
            string value = archiveContent.Trim();
            if (value.Length == 0 || archiveSaving)
            {
                return;
            }

            //先把目标 List 记下来：归档途中用户若点了别的芯片，成功提示也要说对是哪一张
            ArchiveKind target = archiveKind;

            archiveSaving = true;
            AlertService.StartLoading();
            try
            {
                if (!await ArchiveService.ArchiveAsync(target, ArchiveSource.Manual, value))
                {
                    await AlertService.ErrorAsync(I18n.T("Add failed"));
                    return;
                }

                archiveContent = string.Empty;
                await AlertService.SuccessAsync(I18n.T("Archived to {0}", ArchiveKindName(target)));
            }
            finally
            {
                archiveSaving = false;
                AlertService.StopLoading();
            }
        }

        /// <summary>去归档区看刚归档的那一张 List；走之前同样先落地周课散文。</summary>
        private async Task OpenArchiveAsync()
        {
            await SaveIfDirtyAsync();
            To($"archive?kind={(int)archiveKind}");
        }

        private string ArchiveKindName(ArchiveKind kind) => ReviewSectionText.ArchiveKindText(I18n, kind);

        // ———————————————— 读写 ————————————————

        /// <summary>这一周还没开过周课就先建行——目标要挂靠，散文要落库。</summary>
        private async Task<Guid> EnsureReviewIdAsync()
        {
            var current = await EnsureReviewAsync();
            return current.Id;
        }

        private async Task<WeeklyReviewModel> EnsureReviewAsync()
        {
            review ??= await WeeklyReviewService.GetOrCreateAsync(selectedDate);
            return review;
        }

        private Task GoWeekAsync(int offset)
            => SwitchToDateAsync(DateOnly.FromDateTime(selectedDate).AddDays(offset * 7));

        private Task GoCurrentWeekAsync()
            => SwitchToDateAsync(DateOnly.FromDateTime(DateTime.Today));

        /// <summary>
        /// 换到某一周：先落地改动，再切状态，最后让地址栏跟上。
        /// 走路由而不是只改本地状态——刷新、深链、从别处再进来时才是同一周。
        /// </summary>
        private async Task SwitchToDateAsync(DateOnly date)
        {
            if (date == DateOnly.FromDateTime(selectedDate))
            {
                return;
            }

            await SaveIfDirtyAsync();
            await ApplyDateAsync(date);

            To($"weeklyReview?Date={date:yyyy-MM-dd}", replace: true);
        }

        /// <summary>点顶栏那个日期：开日历。标点数据 <see cref="LoadAsync"/> 里已经取好了。</summary>
        private void OpenDatePickerAsync()
        {
            showDatePicker = true;
        }

        /// <summary>串门去这一月的月课：周课 2.2 提炼出的那条绊脚石正是月课 3.1 的素材。</summary>
        private async Task GoMonthlyAsync()
        {
            await SaveIfDirtyAsync();

            To($"monthlyReview?Date={selectedDate:yyyy-MM-dd}");
        }

        /// <summary>
        /// 跳过 / 撤销跳过这一周。只动那个旗子，不碰内容；补写之后
        /// <see cref="IWeeklyReviewService.SaveAsync"/> 会自动把它取消掉。
        /// </summary>
        private async Task ToggleSkipAsync()
        {
            var current = await EnsureReviewAsync();
            bool skipped = current.IsSkipped != true;

            if (!await WeeklyReviewService.SetSkippedAsync(current, skipped))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            await LoadAsync();
        }

        private async Task SaveIfDirtyAsync()
        {
            if (!dirty)
            {
                return;
            }

            await SaveAsync(false);
        }

        private async Task LeavePageSaveAsync()
        {
            NavigateController.RemoveHistoryAction(LeavePageSaveAsync);
            await SaveIfDirtyAsync();
        }

        /// <summary>2.6 之后那一下：把这一周的周课立为已完成（需求 14）。</summary>
        private async Task FinishAsync()
        {
            AlertService.StartLoading();

            try
            {
                var current = await EnsureReviewAsync();
                if (!await WeeklyReviewService.FinalizeAsync(current, sections))
                {
                    await AlertService.ErrorAsync(I18n.T("Change failed"));
                    return;
                }

                dirty = false;
                await InvokeAsync(StateHasChanged);
            }
            finally
            {
                AlertService.StopLoading();
            }
        }

        private async Task SaveAsync(bool navigateBack)
        {
            AlertService.StartLoading();

            try
            {
                //只有真的要写散文时才建周课记录；纯浏览不该留下空行
                if (!sections.IsEmpty)
                {
                    var current = await EnsureReviewAsync();
                    if (!await WeeklyReviewService.SaveAsync(current, sections))
                    {
                        await AlertService.ErrorAsync(I18n.T("Change failed"));
                        return;
                    }
                }

                //归因与散文各存各的：散文可能一个字没写，但 2.1 的归因得落地
                await PersistDiagnosesAsync();

                dirty = false;

                if (navigateBack)
                {
                    await NavigateToBack();
                }
            }
            finally
            {
                AlertService.StopLoading();
            }
        }

        // ———————————————— 展示 ————————————————

        //这三个映射与「写进正文的生成式摘要」共用同一份实现（见 ReviewSectionText），
        //页面上写着「环境原因」而导出的正文里是别的词，是最难发现的那类不一致。
        private string DomainText(GoalDomain domain) => ReviewSectionText.DomainText(I18n, domain);

        private string VerdictText(BlockVerdict verdict) => ReviewSectionText.VerdictText(I18n, verdict);

        private string StatusText(GoalStatus status) => ReviewSectionText.StatusText(I18n, status);

        /// <summary>这一周的区间。2.1 结算的就是这一周，所以顶栏与 2.1 都显示它。</summary>
        private string WeekRange => $"{weekStart:yyyy/MM/dd} - {weekEnd:MM/dd}";

        /// <summary>下一周的区间。2.6「下周三个目标」显示它，让人看清目标落在哪一周。</summary>
        private string NextWeekRange => $"{nextWeekStart:yyyy/MM/dd} - {nextWeekEnd:MM/dd}";
    }
}
