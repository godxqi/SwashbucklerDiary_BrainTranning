using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Models;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 月课（方法文档 3）。文档对月记的风险写得最直接：<b>「月记做完如果没有出口，就只是写完了」</b>，
    /// 所以这里做成<b>四步带闸门的向导</b>——每一步没有产出就点不动「下一步」：
    /// <list type="number">
    /// <item>3.1 三列审视（最多六条）：限制性观念 / 支持性证据 / 反面证据 / 效用</item>
    /// <item>3.2 出口：把第三列的反对性陈述提炼成短语，进自我肯定库（3–4 条）</item>
    /// <item>3.3 自我补偿——书里明说别跳过，这是全系统唯一一个专门挖痛处的动作</item>
    /// <item>3.4 三列三行敌人表：针对<b>目标</b>，不是针对信念</item>
    /// </list>
    /// <para>
    /// 点选与新增立刻落库；只有 3.3 那段文本走「脏了才存 + 返回键保存」。
    /// 底部的「完成本月月课」是节拍判定的唯一开关——不点亮它，复盘主页会把这一月永远算成「已逾期」。
    /// </para>
    /// </summary>
    public partial class MonthlyReviewPage : ImportantComponentBase
    {
        /// <summary>四步的步数。与「摘要要渲染几节」是同一件事，别再各写一个 4。</summary>
        private const int StepCount = MonthlyReviewSections.MaxSection;

        /// <summary>文档 3.1：限制性观念「最多六种」。</summary>
        private const int MaxBeliefs = MonthlyReviewSections.MaxBeliefs;

        /// <summary>文档 3.4：敌人表是三列三行。</summary>
        private const int MaxEnemies = MonthlyReviewSections.MaxEnemies;

        /// <summary>文档 3.2：「我通常觉得三四个就足够了」——只是个建议，不拦人。</summary>
        private const int SuggestedAffirmations = MonthlyReviewSections.SuggestedAffirmationCount;

        /// <summary>文档 5.2：同类触因攒 3 个就该交到 3.1 的第一列。</summary>
        private const int TriggerThreshold = 3;

        /// <summary>本月某类微观失败出现几次才算「反复出现的绊脚石」。与周课 2.1 同一个门槛。</summary>
        private const int FrequentThreshold = 2;

        private DateTime selectedDate = DateTime.Now.Date;

        /// <summary>
        /// 已经落地过的那个日期（来自 <c>?Date=</c>）。用它判断参数到底变没变——
        /// 父组件重渲染也会走 <c>OnParametersSetAsync</c>，不挡一道就会反复重载。
        /// </summary>
        private DateOnly? appliedDate;

        private bool showDatePicker;

        private bool showMenu;

        /// <summary>写过日课的日子。桌面端日历标点用；手机端标不了点（PMobileDatePicker 没有 Events）。</summary>
        private DateOnly[] eventsDates = [];

        private List<DynamicListItem> menuItems = [];

        /// <summary>日历的可选上界：补的是过去，写不了未来。</summary>
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        /// <summary>日历上显示的那个值。只读——换日期一律走 <see cref="SwitchToDateAsync"/>。</summary>
        private DateOnly SelectedDate => DateOnly.FromDateTime(selectedDate);

        /// <summary>⋮ 里那一项该写「跳过」还是「撤销跳过」。</summary>
        private string SkipLabelKey => review?.IsSkipped == true ? "Unskip this month" : "Skip this month";

        private int year;

        private int month;

        private DateTime monthStart;

        private DateTime monthEnd;

        /// <summary>null = 这一月还没开过月课。只有真的要落东西时才建行。</summary>
        private MonthlyReviewModel? review;

        private List<LimitingBeliefModel> beliefs = [];

        private List<EnemyModel> enemies = [];

        /// <summary>本次月课产出的肯定短语（3.2 与 3.4 打击手段①）。</summary>
        private List<SelfAffirmationModel> producedAffirmations = [];

        /// <summary>3.1 的素材之一：攒够 3 次的触因（文档 5.2）。</summary>
        private List<TriggerCount> readyTriggers = [];

        /// <summary>3.1 的素材之三：这一月反复出现的课题类型。</summary>
        private List<LessonTypeCount> lessonTypeCandidates = [];

        /// <summary>3.1 的素材之四：这一月反复出现的人物。</summary>
        private List<LessonPersonCount> lessonPersonCandidates = [];

        /// <summary>3.1 的素材之二：这一月反复出现的绊脚石。</summary>
        private List<StumblingBlockView> monthBlocks = [];

        private string[] monthlyTitles = [];

        private string[] monthlyDescriptions = [];

        private int step = 1;

        private MonthlyStepGates gates = MonthlyStepGates.None;

        private string step33Note = string.Empty;

        private bool dirty;

        private bool showBeliefDialog;

        private LimitingBeliefModel? editingBelief;

        private bool showDeleteBelief;

        private LimitingBeliefModel? pendingDeleteBelief;

        private bool showEnemyDialog;

        private EnemyModel? editingEnemy;

        private bool showDeleteEnemy;

        private EnemyModel? pendingDeleteEnemy;

        private bool showPhraseDialog;

        /// <summary>3.2 里正在被提炼成短语的那条反面证据；null 表示直接手写一条。</summary>
        private LimitingBeliefModel? phraseSource;

        private bool showDeleteAffirmation;

        private SelfAffirmationModel? pendingDeleteAffirmation;

        /// <summary>3.3 的自我补偿之③：把人加进积极列表（6.1）。</summary>
        private bool showAddPositive;

        /// <summary>3.4 打击手段③：把「本周可完成的具体操作」带进下周 2.3。</summary>
        private bool showGoalDialog;

        private WeeklyGoalModel? editingGoal;

        /// <summary>3.4 要落进哪一周。按下「带进下周目标」时记住，真正保存时才建那一周的周课。</summary>
        private DateTime? pendingGoalWeek;

        [Inject]
        private IMonthlyReviewService MonthlyReviewService { get; set; } = default!;

        [Inject]
        private IStumblingBlockService StumblingBlockService { get; set; } = default!;

        [Inject]
        private ITriggerService TriggerService { get; set; } = default!;

        [Inject]
        private IReviewScheduleService ReviewScheduleService { get; set; } = default!;

        [Inject]
        private IWeeklyReviewService WeeklyReviewService { get; set; } = default!;

        [Inject]
        private IVirtueService VirtueService { get; set; } = default!;

        [Inject]
        private IAffirmationService AffirmationService { get; set; } = default!;

        [Inject]
        private IArchiveService ArchiveService { get; set; } = default!;

        [Inject]
        private IDailyReviewService DailyReviewService { get; set; } = default!;

        [Inject]
        private ILessonService LessonService { get; set; } = default!;

        /// <summary>复盘主页点进来的那一月。缺省就是当前该做的那一月。</summary>
        [SupplyParameterFromQuery]
        private DateOnly? Date { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //用返回键离开时也要落地，否则「3.3 写了一半按返回」等于白写
            NavigateController.AddHistoryAction(LeavePageSaveAsync, false);

            menuItems =
            [
                new(this, "Back to today", "today", GoCurrentMonthAsync),
                new(this, () => SkipLabelKey, "block", ToggleSkipAsync),
            ];
        }

        /// <summary>
        /// 日期一律从这里落地，<b>不放在 <c>OnInitializedAsync</c> 里</b>。
        /// 页面是被缓存的：从复盘主页 / ⋮ 菜单带着新的 <c>?Date=</c> 再进来时，
        /// 初始化只跑一次，光靠它读日期会停在上一次那一月。
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

        /// <summary>切到某一月：换状态、重新读。地址栏由调用方另行同步。</summary>
        private async Task ApplyDateAsync(DateOnly date)
        {
            appliedDate = date;
            selectedDate = date.ToDateTime(TimeOnly.MinValue).Date;

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
            monthlyTitles = ReviewSectionText.MonthlyTitles(I18n);
            monthlyDescriptions = ReviewSectionText.MonthlyDescriptions(I18n);

            var range = ReviewScheduleService.GetMonth(selectedDate);
            monthStart = range.Start;
            monthEnd = range.End;
            year = range.Year;
            month = range.Month;

            review = await MonthlyReviewService.GetAsync(year, month);
            beliefs = await MonthlyReviewService.GetBeliefsAsync(review?.Id ?? Guid.Empty);
            enemies = await MonthlyReviewService.GetEnemiesAsync(review?.Id ?? Guid.Empty);
            producedAffirmations = await AffirmationService
                .GetByMonthlyReviewAsync(review?.Id ?? Guid.Empty);
            readyTriggers = await TriggerService.GetReadyForMonthlyAsync(TriggerThreshold);
            monthBlocks = await StumblingBlockService.GetByMonthAsync(year, month);
            lessonTypeCandidates = await LessonService.GetMonthlyCandidateTypesAsync(monthStart);
            lessonPersonCandidates = await LessonService.GetMonthlyCandidatePersonsAsync(monthStart);

            step33Note = review?.Step33Note ?? string.Empty;

            //写过日课的日子：桌面端日历标点用
            eventsDates = [.. (await DailyReviewService.GetReviewDatesAsync()).Select(DateOnly.FromDateTime)];

            dirty = false;

            await RefreshGatesAsync();

            //换月之后回到第一步；月课是从头往下走的。
            //已经定稿的月例外：那是回过去看结果 / 改结果，落到最后一步才对
            //（闸门对已定稿的月本来就是放开的，落在第 1 步等于让人白点三下）。
            step = review?.IsFinalized == true ? StepCount : 1;

            await InvokeAsync(StateHasChanged);
        }

        // ———————————————— 步骤与闸门 ————————————————

        private async Task RefreshGatesAsync()
        {
            var loaded = await MonthlyReviewService.GetGatesAsync(review);
            //3.3 的闸门看的是文本框里的实时内容，不是库里那份
            gates = loaded with { Step33 = !string.IsNullOrWhiteSpace(step33Note) };

            await InvokeAsync(StateHasChanged);
        }

        /// <summary>能不能跳到第 <paramref name="target"/> 步：前面每一步都得先有产出。</summary>
        private bool CanEnter(int target)
        {
            if (target <= 1)
            {
                return true;
            }

            //已经定稿的月：闸门是当初用来引导「一路走完」的，不该反过来把人锁在门外。
            //否则回过去改一个已完成的月时，只要底层数据被动过（删了条失败行之类），
            //就再也进不到第 4 步，等于把这个月封死了。
            if (review?.IsFinalized == true)
            {
                return true;
            }

            if (target >= 2 && !gates.Step31)
            {
                return false;
            }

            if (target >= 3 && !gates.Step32)
            {
                return false;
            }

            return target < 4 || gates.Step33;
        }

        private bool GatePassed(int atStep) => atStep switch
        {
            1 => gates.Step31,
            2 => gates.Step32,
            3 => gates.Step33,
            _ => gates.Step34,
        };

        /// <summary>还差什么。闸门拦住人的时候得说清楚，不能只是把按钮变灰。</summary>
        private string GateHint(int atStep) => I18n.T(atStep switch
        {
            1 => "Write at least one limiting belief.",
            2 => "Put at least one phrase into the affirmation library.",
            3 => "Write down your self-compensation - this step cannot be skipped.",
            _ => "Fill in at least one row of the enemy table.",
        });

        private async Task GoStepAsync(int delta)
        {
            await SaveIfDirtyAsync();

            int target = Math.Clamp(step + delta, 1, StepCount);
            if (target != step && CanEnter(target))
            {
                step = target;
            }

            await InvokeAsync(StateHasChanged);
        }

        private async Task GoToStepAsync(int target)
        {
            if (!CanEnter(target))
            {
                return;
            }

            await SaveIfDirtyAsync();

            step = target;
            await InvokeAsync(StateHasChanged);
        }

        // ———————————————— 3.1 三列审视 ————————————————

        /// <summary>本月反复出现的类别（≥2 次）——文档 3.1 说的「这一月反复出现的绊脚石」。</summary>
        private List<(string Category, int Count)> FrequentCategories
        {
            get
            {
                return [.. monthBlocks
                    .Where(it => !string.IsNullOrWhiteSpace(it.Block.Category))
                    .GroupBy(it => it.Block.Category!)
                    .Select(it => (Category: it.Key, Count: it.Count()))
                    .Where(it => it.Count >= FrequentThreshold)
                    .OrderByDescending(it => it.Count)
                    .ThenBy(it => it.Category, StringComparer.CurrentCulture)];
            }
        }

        private void OpenBeliefDialog(LimitingBeliefModel? belief)
        {
            editingBelief = belief;
            showBeliefDialog = true;
        }

        /// <summary>把素材（触因）起成一条限制性观念：先预填名字，由使用者改写。</summary>
        private void UseTriggerAsBelief(TriggerCount trigger)
        {
            OpenBeliefDialog(new LimitingBeliefModel
            {
                MonthlyReviewId = review?.Id ?? Guid.Empty,
                Assertion = trigger.Tag.Name,
            });
        }

        /// <summary>把素材（本月反复出现的类别）起成一条限制性观念。</summary>
        private void UseCategoryAsBelief(string category)
        {
            OpenBeliefDialog(new LimitingBeliefModel
            {
                MonthlyReviewId = review?.Id ?? Guid.Empty,
                Assertion = category,
            });
        }

        private void UseLessonTypeAsBelief(LessonTypeCount item)
        {
            OpenBeliefDialog(new LimitingBeliefModel
            {
                MonthlyReviewId = review?.Id ?? Guid.Empty,
                Assertion = I18n.T(item.Type.ToString()),
            });
        }

        private void UseLessonPersonAsBelief(LessonPersonCount item)
        {
            OpenBeliefDialog(new LimitingBeliefModel
            {
                MonthlyReviewId = review?.Id ?? Guid.Empty,
                Assertion = item.PersonName ?? string.Empty,
            });
        }

        private async Task SaveBeliefAsync(LimitingBeliefModel belief)
        {
            showBeliefDialog = false;

            belief.MonthlyReviewId = await EnsureReviewIdAsync();

            if (!await MonthlyReviewService.SaveBeliefAsync(belief))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            beliefs = await MonthlyReviewService.GetBeliefsAsync(belief.MonthlyReviewId);
            await RefreshGatesAsync();
        }

        private void AskDeleteBelief(LimitingBeliefModel belief)
        {
            pendingDeleteBelief = belief;
            showDeleteBelief = true;
        }

        private async Task ConfirmDeleteBeliefAsync()
        {
            showDeleteBelief = false;

            var target = pendingDeleteBelief;
            pendingDeleteBelief = null;
            if (target is null)
            {
                return;
            }

            if (!await MonthlyReviewService.DeleteBeliefAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            beliefs.RemoveAll(it => it.Id == target.Id);
            await RefreshGatesAsync();
        }

        /// <summary>归档区 7.1「对自我的新认识」的入口（文档 7.1）：三列审视的产出。</summary>
        private async Task ArchiveBeliefAsync(LimitingBeliefModel belief)
        {
            if (!await ArchiveService.ArchiveAsync(ArchiveKind.SelfInsight,
                    ArchiveSource.SelfInsight, belief.Assertion))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            await AlertService.SuccessAsync(I18n.T("Archived to list 7.1"));
        }

        // ———————————————— 3.2 出口 ————————————————

        /// <summary>有反面证据的那些观念——3.2 的原料就是它们。</summary>
        private List<LimitingBeliefModel> CounterEvidenceSources
            => [.. beliefs.Where(it => !string.IsNullOrWhiteSpace(it.CounterEvidence))];

        private void OpenPhraseDialog(LimitingBeliefModel? source)
        {
            phraseSource = source;
            showPhraseDialog = true;
        }

        private async Task AddPhraseAsync(SelfAffirmationModel affirmation)
        {
            showPhraseDialog = false;

            var current = await EnsureReviewAsync();
            //「来自哪」默认落成那条限制性观念，使用者可以改
            string? origin = string.IsNullOrWhiteSpace(affirmation.Source)
                ? phraseSource?.Assertion
                : affirmation.Source;

            if (!await MonthlyReviewService.AddAffirmationAsync(current, affirmation.Content, origin))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            producedAffirmations = await AffirmationService.GetByMonthlyReviewAsync(current.Id);
            await RefreshGatesAsync();
        }

        private void AskDeleteAffirmation(SelfAffirmationModel affirmation)
        {
            pendingDeleteAffirmation = affirmation;
            showDeleteAffirmation = true;
        }

        private async Task ConfirmDeleteAffirmationAsync()
        {
            showDeleteAffirmation = false;

            var target = pendingDeleteAffirmation;
            pendingDeleteAffirmation = null;
            if (target is null)
            {
                return;
            }

            if (!await AffirmationService.DeleteAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            producedAffirmations.RemoveAll(it => it.Id == target.Id);
            await RefreshGatesAsync();
        }

        // ———————————————— 3.3 自我补偿 ————————————————

        private async Task AddPositiveAsync(string name)
        {
            showAddPositive = false;

            if (!await VirtueService.AddAsync(VirtueKind.Positive, name))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            await AlertService.SuccessAsync(I18n.T("Added to the positive list"));
        }

        // ———————————————— 3.4 敌人表 ————————————————

        private void OpenEnemyDialog(EnemyModel? enemy)
        {
            editingEnemy = enemy;
            showEnemyDialog = true;
        }

        private async Task SaveEnemyAsync(EnemyModel enemy)
        {
            showEnemyDialog = false;

            enemy.MonthlyReviewId = await EnsureReviewIdAsync();

            if (!await MonthlyReviewService.SaveEnemyAsync(enemy))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            enemies = await MonthlyReviewService.GetEnemiesAsync(enemy.MonthlyReviewId);
            await RefreshGatesAsync();
        }

        private void AskDeleteEnemy(EnemyModel enemy)
        {
            pendingDeleteEnemy = enemy;
            showDeleteEnemy = true;
        }

        private async Task ConfirmDeleteEnemyAsync()
        {
            showDeleteEnemy = false;

            var target = pendingDeleteEnemy;
            pendingDeleteEnemy = null;
            if (target is null)
            {
                return;
            }

            if (!await MonthlyReviewService.DeleteEnemyAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            enemies.RemoveAll(it => it.Id == target.Id);
            await RefreshGatesAsync();
        }

        /// <summary>
        /// 打击手段③（文档 3.4 回环总图里那条 M34 → 2.3 的边）：把「本周可完成的具体操作」
        /// 带进<b>下周</b>的三个目标里。目标卡要挂在下周的周课记录上，所以先把它取出来（没有就建）。
        /// </summary>
        private async Task PushToNextWeekAsync(EnemyModel enemy)
        {
            if (string.IsNullOrWhiteSpace(enemy.WeeklyAction))
            {
                await AlertService.WarningAsync(I18n.T("Write the concrete action first."));
                return;
            }

            //这里只记住目标该落到哪一周，不建那一周的周课记录：
            //只是打开对话框看一眼又取消的话，不该凭空多出一条空周课。
            //基准是「正在看的那个月」，不是「现在」——补 7 月的月课时，目标要落进 7 月的下周，
            //早先写死 DateTime.Now 会让它悄悄挂到当前周上。
            pendingGoalWeek = ReviewScheduleService.GetWeek(selectedDate).Start.AddDays(7);

            editingGoal = new WeeklyGoalModel
            {
                Domain = GoalDomain.PersonalDevelopment,
                Content = enemy.WeeklyAction.Trim(),
            };

            showGoalDialog = true;
        }

        private async Task SaveGoalAsync(WeeklyGoalModel goal)
        {
            showGoalDialog = false;

            //目标卡要挂在下周的周课记录上；到这一步（用户确实保存了）才去建它
            if (goal.WeeklyReviewId == Guid.Empty)
            {
                if (pendingGoalWeek is not DateTime week)
                {
                    return;
                }

                goal.WeeklyReviewId = (await WeeklyReviewService.GetOrCreateAsync(week)).Id;
                pendingGoalWeek = null;
            }

            if (!await WeeklyReviewService.SaveGoalAsync(goal))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            await AlertService.SuccessAsync(I18n.T("Carried into next week's goals"));
        }

        // ———————————————— 读写 ————————————————

        private async Task<Guid> EnsureReviewIdAsync()
        {
            var current = await EnsureReviewAsync();
            return current.Id;
        }

        private async Task<MonthlyReviewModel> EnsureReviewAsync()
        {
            review ??= await MonthlyReviewService.GetOrCreateAsync(selectedDate);
            return review;
        }

        private string StepClass(int atStep)
        {
            if (atStep == step)
            {
                return "review-chip review-chip--on";
            }

            return GatePassed(atStep) ? "review-chip review-chip--done" : "review-chip";
        }

        /// <summary>步骤芯片上的字：编号 + 阶段名，一眼看得出走到哪、下一步是什么。</summary>
        private string StepChipLabel(int atStep)
            => $"{atStep}. {monthlyTitles.ElementAtOrDefault(atStep - 1)}";

        private void MarkDirty()
        {
            dirty = true;

            //3.3 的闸门跟着文本框实时变
            gates = gates with { Step33 = !string.IsNullOrWhiteSpace(step33Note) };
        }

        private Task GoMonthAsync(int offset)
            => SwitchToDateAsync(DateOnly.FromDateTime(selectedDate).AddMonths(offset));

        private Task GoCurrentMonthAsync()
            => SwitchToDateAsync(DateOnly.FromDateTime(DateTime.Today));

        /// <summary>
        /// 换到某一月：先落地改动，再切状态，最后让地址栏跟上。
        /// 走路由而不是只改本地状态——刷新、深链、从别处再进来时才是同一月。
        /// </summary>
        private async Task SwitchToDateAsync(DateOnly date)
        {
            if (date == DateOnly.FromDateTime(selectedDate))
            {
                return;
            }

            await SaveIfDirtyAsync();
            await ApplyDateAsync(date);

            To($"monthlyReview?Date={date:yyyy-MM-dd}", replace: true);
        }

        /// <summary>点顶栏那个日期：开日历。标点数据 <see cref="LoadAsync"/> 里已经取好了。</summary>
        private void OpenDatePickerAsync()
        {
            showDatePicker = true;
        }

        /// <summary>
        /// 跳过 / 撤销跳过这一月。只动那个旗子，不碰内容；补写之后
        /// <see cref="IMonthlyReviewService.SaveAsync"/> 会自动把它取消掉。
        /// </summary>
        private async Task ToggleSkipAsync()
        {
            var current = await EnsureReviewAsync();
            bool skipped = current.IsSkipped != true;

            if (!await MonthlyReviewService.SetSkippedAsync(current, skipped))
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

            await SaveAsync();
        }

        private async Task LeavePageSaveAsync()
        {
            NavigateController.RemoveHistoryAction(LeavePageSaveAsync);
            await SaveIfDirtyAsync();
        }

        private async Task SaveAsync()
        {
            AlertService.StartLoading();

            try
            {
                //3.3 打了字又清空：既然什么都没留下，就不该为这一次编辑凭空建一条空月课行
                if (review is null && string.IsNullOrWhiteSpace(step33Note))
                {
                    dirty = false;
                    await RefreshGatesAsync();
                    return;
                }

                var current = await EnsureReviewAsync();
                if (!await MonthlyReviewService.SaveAsync(current, step33Note))
                {
                    await AlertService.ErrorAsync(I18n.T("Change failed"));
                    return;
                }

                dirty = false;
                await RefreshGatesAsync();
            }
            finally
            {
                AlertService.StopLoading();
            }
        }

        /// <summary>3.4 之后那一下：把这一月的月课立为已完成（需求 14）。</summary>
        private async Task FinishAsync()
        {
            if (!gates.AllDone)
            {
                await AlertService.WarningAsync(GateHint(FirstUnfinishedStep()));
                return;
            }

            AlertService.StartLoading();

            try
            {
                var current = await EnsureReviewAsync();
                if (!await MonthlyReviewService.FinalizeAsync(current, step33Note))
                {
                    await AlertService.ErrorAsync(I18n.T("Change failed"));
                    return;
                }

                dirty = false;
                await RefreshGatesAsync();
            }
            finally
            {
                AlertService.StopLoading();
            }
        }

        private int FirstUnfinishedStep()
        {
            for (int i = 1; i <= StepCount; i++)
            {
                if (!GatePassed(i))
                {
                    return i;
                }
            }

            return StepCount;
        }

        // ———————————————— 展示 ————————————————

        private string MonthRange => $"{monthStart:yyyy/MM}";

        private string StepLabel(int atStep) => $"{atStep}. {monthlyTitles.ElementAtOrDefault(atStep - 1)}";

        private string StepDescription(int atStep) => monthlyDescriptions.ElementAtOrDefault(atStep - 1) ?? string.Empty;

        private string AffirmationHint => $"{I18n.T("Affirmation library")} · {producedAffirmations.Count}/{SuggestedAffirmations}";

        private string BeliefCountHint => $"{beliefs.Count}/{MaxBeliefs}";

        private string EnemyCountHint => $"{enemies.Count}/{MaxEnemies}";
    }
}
