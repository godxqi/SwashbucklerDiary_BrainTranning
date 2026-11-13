using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Models;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 日课（方法文档 1）。<b>严格按文档的章节顺序</b>排：1.1 生活上的 → 1.2 学习上的 →
    /// 1.3 今天做的三项积极的 → 1.4 微观失败 → 1.5 精力 / 分心 / 疲劳 → 1.6 情绪命名 →
    /// 1.7 今天重复了哪一条自我肯定。一节都不缺，每节都写明这一格该填什么。
    /// <para>
    /// 1.3 固定三条、1.4 固定四问，都预留好分框。散文写回日记正文的章节里，
    /// 芯片的产物写进正文末尾的受管区块，两者互不覆盖。正文本身仍是普通 Markdown，
    /// 在「库表」里可以用完整编辑器打开改。
    /// </para>
    /// </summary>
    public partial class DailyReviewPage : ImportantComponentBase
    {
        /// <summary>超过这个像素的横向拖动才算「滑了一下」，避免和点击抢。</summary>
        private const double swipeThreshold = 60;

        private static readonly MoodKind[] moods = Enum.GetValues<MoodKind>();

        private DateTime selectedDate = DateTime.Now.Date;

        /// <summary>
        /// 已经落地过的那个日期（来自 <c>?Date=</c>）。用它判断参数到底变没变——
        /// 父组件重渲染也会走 <c>OnParametersSetAsync</c>，不挡一道就会反复重载。
        /// </summary>
        private DateOnly? appliedDate;

        private DailyReviewModel review = new();

        /// <summary>1.1 ~ 1.5 的散文。正文（DiaryModel.Content）是它的唯一真源。</summary>
        private DailyReviewSections sections = new();

        private string[] dailyTitles = [];

        /// <summary>各节的「这要填什么」，取自方法文档对应章节。</summary>
        private string[] dailyDescriptions = [];

        private string[] microFailureQuestions = [];

        private List<SelfAffirmationModel> affirmations = [];

        private List<Guid> selectedAffirmationIds = [];

        private bool showAddAffirmation;

        private bool showDeleteAffirmation;

        /// <summary>等着确认删除的那条肯定。确认框是共用的，所以先记下来。</summary>
        private SelfAffirmationModel? pendingDeleteAffirmation;

        private bool showDatePicker;

        private bool showDeleteDailyReview;

        private bool showMenu;

        private List<DynamicListItem> menuItems = [];

        /// <summary>这一天还什么都没写过。给日历标点的手机端补一句提示（手机端标不了点）。</summary>
        private bool blankDay;

        /// <summary>写过日课的日子。只给桌面端的日历标点用，点开日历时才去查。</summary>
        private DateOnly[] eventsDates = [];

        /// <summary>
        /// 这一天所在那一周的三个目标。<b>书里 ch12 把「每周三目标」叫作「日记的骨架」</b>
        /// （原话：「每日三项积极事情 + 每周三目标是它的最小可运行结构」），
        /// 所以写日课的时候得看得见它们——这里只提示，改目标和结算都在周课 2.3 / 2.4。
        /// </summary>
        private List<WeeklyGoalModel> weekGoals = [];

        private bool dirty;

        private double? pointerStartX;

        /// <summary>刚才是「滑了一下」而不是「点了一下」，用来把随后的 click 吃掉。</summary>
        private bool swipeHandled;

        /// <summary>日历的可选上界：日课补的是过去，写不了未来。</summary>
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        /// <summary>日历上显示的那个值。只读——换日期一律走 <see cref="SwitchToDateAsync"/>（要先落地、还要同步地址栏）。</summary>
        private DateOnly SelectedDate => DateOnly.FromDateTime(selectedDate);

        [Inject]
        private IDailyReviewService DailyReviewService { get; set; } = default!;

        [Inject]
        private IAffirmationService AffirmationService { get; set; } = default!;

        [Inject]
        private IGlobalConfiguration GlobalConfiguration { get; set; } = default!;

        [Inject]
        private IWeeklyReviewService WeeklyReviewService { get; set; } = default!;

        [Inject]
        private IReviewScheduleService ReviewScheduleService { get; set; } = default!;

        [SupplyParameterFromQuery]
        private DateOnly? Date { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //用返回键离开时也要落地，否则「写了一半按返回」等于白写
            NavigateController.AddHistoryAction(LeavePageSaveAsync, false);

            menuItems =
            [
                new(this, "Back to today", "today", GoTodayAsync),
                new(this, "This week's weekly review", "event_note", GoWeeklyAsync),
                new(this, "This month's monthly review", "calendar_month", GoMonthlyAsync),
                new(this, "Delete this daily review", "delete", AskDeleteDailyReviewAsync, () => !blankDay),
            ];
        }

        /// <summary>
        /// 日期一律从这里落地，<b>不放在 <c>OnInitializedAsync</c> 里</b>。
        /// 页面是被缓存的：从复盘主页 / ⋮ 菜单带着新的 <c>?Date=</c> 再进来时，
        /// 初始化只跑一次，光靠它读日期会停在上一次那一天。
        /// </summary>
        protected override async Task OnParametersSetAsync()
        {
            var target = Date ?? DateOnly.FromDateTime(DateTime.Now);

            if (appliedDate != target)
            {
                //换日期之前先把没存的改动落地
                if (appliedDate is not null)
                {
                    await SaveIfDirtyAsync();
                }

                await ApplyDateAsync(target);
            }

            await base.OnParametersSetAsync();
        }

        /// <summary>切到某一天：换状态、重新读。地址栏由调用方另行同步。</summary>
        private async Task ApplyDateAsync(DateOnly date)
        {
            appliedDate = date;
            selectedDate = date.ToDateTime(TimeOnly.MinValue).Date;

            await LoadAsync();
        }

        protected override async Task OnResume()
        {
            //回到本页时先保住没存下的改动，再重载
            await SaveIfDirtyAsync();
            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            dailyTitles = ReviewSectionText.DailyTitles(I18n);
            dailyDescriptions = ReviewSectionText.DefaultDailyDescriptions(I18n);
            microFailureQuestions = ReviewSectionText.MicroFailureQuestions(I18n);

            affirmations = await AffirmationService.GetActiveAsync();

            //这一天的周课目标。目标卡挂在「它要完成的那一周」的周课记录上，
            //所以按这一天的 ISO 周去取；那一周还没开过周课就返回空表（那是「没定目标」，不是错误）。
            var week = ReviewScheduleService.GetWeek(selectedDate);
            weekGoals = [.. (await WeeklyReviewService.GetGoalsAsync(week.Year, week.WeekOfYear))
                .Where(it => !string.IsNullOrWhiteSpace(it.Content))];

            review = await DailyReviewService.GetByDateAsync(selectedDate)
                ?? new DailyReviewModel { Date = selectedDate };
            selectedAffirmationIds = review.GetAffirmationIds();

            var diary = await DailyReviewService.GetDiaryByDateAsync(selectedDate);
            sections = diary is null
                ? new DailyReviewSections()
                : DailyReviewSections.Parse(ReviewContentBlock.Strip(diary.Content));

            //结构化的行和那篇日记都不在，才是「这天还没写过」——
            //只看日记会误判：清空日课后日记还在，那不算空白。
            blankDay = diary is null && review.Id == Guid.Empty;

            dirty = false;

            await InvokeAsync(StateHasChanged);
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void SelectMood(MoodKind mood)
        {
            review.Mood = review.Mood == mood ? null : mood;
            dirty = true;
        }

        /// <summary>再加一条微观失败。四问是针对一个失败的，一天可以有好几条。</summary>
        private void AddMicroFailure()
        {
            sections.MicroFailures.Add(DailyReviewSections.NewMicroFailure());
            dirty = true;
        }

        private void RemoveMicroFailure(int index)
        {
            //至少留一条，不然这一节就没了
            if (sections.MicroFailures.Count <= 1 || index < 0 || index >= sections.MicroFailures.Count)
            {
                return;
            }

            sections.MicroFailures.RemoveAt(index);
            dirty = true;
        }

        private void ToggleAffirmation(SelfAffirmationModel affirmation)
        {
            if (!selectedAffirmationIds.Remove(affirmation.Id))
            {
                selectedAffirmationIds.Add(affirmation.Id);
            }

            dirty = true;
        }

        private async Task AddAffirmationAsync(string content)
        {
            showAddAffirmation = false;

            string value = content?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return;
            }

            var affirmation = new SelfAffirmationModel { Id = Guid.NewGuid(), Content = value };
            if (!await AffirmationService.AddAsync(affirmation))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            selectedAffirmationIds.Add(affirmation.Id);
            dirty = true;

            affirmations = await AffirmationService.GetActiveAsync();
        }

        /// <summary>点芯片上的 ✕：先问一句，删掉的是整条肯定库记录，不是只从今天拿掉。</summary>
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

            //今天本来选了它的话，得从选中集合里摘掉，否则保存时会写进一个已经不存在的 Id
            if (selectedAffirmationIds.Remove(target.Id))
            {
                dirty = true;
            }

            affirmations.RemoveAll(it => it.Id == target.Id);

            await InvokeAsync(StateHasChanged);
        }

        private void HandlePointerDown(PointerEventArgs e)
        {
            //每次手势开始时先复位：万一上一次「滑动后的 click」没送达（手指划出元素外就会这样），
            //标记会一直挂着，把随后的第一次点击白白吃掉。
            swipeHandled = false;

            //只认触摸：桌面端拖着鼠标划过标题不该切日期
            pointerStartX = string.Equals(e.PointerType, "touch", StringComparison.OrdinalIgnoreCase)
                ? e.ClientX
                : null;
        }

        private async Task HandlePointerUpAsync(PointerEventArgs e)
        {
            if (pointerStartX is not double startX)
            {
                return;
            }

            pointerStartX = null;

            double offset = e.ClientX - startX;
            if (Math.Abs(offset) < swipeThreshold)
            {
                return;
            }

            //向左滑看下一天，向右滑看上一天。
            //标一手「这次是滑动」——不标的话紧随其后的 click 会顺手把日历弹出来。
            swipeHandled = true;

            await GoDayAsync(offset < 0 ? 1 : -1);
        }

        private Task GoDayAsync(int offset)
            => SwitchToDateAsync(DateOnly.FromDateTime(selectedDate).AddDays(offset));

        private Task GoTodayAsync()
            => SwitchToDateAsync(DateOnly.FromDateTime(DateTime.Today));

        /// <summary>
        /// 换到某一天：先落地改动，再切状态，最后让地址栏跟上。
        /// 走路由而不是只改本地状态——刷新、深链、从别处再进来时才是同一天。
        /// </summary>
        private async Task SwitchToDateAsync(DateOnly date)
        {
            if (date == DateOnly.FromDateTime(selectedDate))
            {
                return;
            }

            await SaveIfDirtyAsync();
            await ApplyDateAsync(date);

            To($"dailyReview?Date={date:yyyy-MM-dd}", replace: true);
        }

        /// <summary>
        /// 点顶栏那个日期：开日历。标点数据这时候才查——平时左右滑日期不用付这个查询。
        /// </summary>
        private async Task OpenDatePickerAsync()
        {
            //刚才是滑动不是点击：把这一次 click 吃掉，不然划一下就弹日历
            if (swipeHandled)
            {
                swipeHandled = false;
                return;
            }

            eventsDates = [.. (await DailyReviewService.GetReviewDatesAsync()).Select(DateOnly.FromDateTime)];

            showDatePicker = true;
        }

        /// <summary>串门去这一周的周课。文档 2.1 的清单就是从这一天的 1.4 长出来的。</summary>
        private async Task GoWeeklyAsync()
        {
            await SaveIfDirtyAsync();

            To($"weeklyReview?Date={selectedDate:yyyy-MM-dd}");
        }

        /// <summary>串门去这一月的月课。</summary>
        private async Task GoMonthlyAsync()
        {
            await SaveIfDirtyAsync();

            To($"monthlyReview?Date={selectedDate:yyyy-MM-dd}");
        }

        private void AskDeleteDailyReviewAsync()
        {
            showDeleteDailyReview = true;
        }

        private async Task ConfirmDeleteDailyReviewAsync()
        {
            showDeleteDailyReview = false;

            if (!await DailyReviewService.DeleteAsync(selectedDate))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            //删完停在同一天，页面上显示成「这天还没写过」
            await LoadAsync();
        }

        /// <summary>换日期 / 离开页面之前，先把改动落地。</summary>
        private async Task SaveIfDirtyAsync()
        {
            if (!dirty)
            {
                return;
            }

            await SaveAsync(false, false);
        }

        /// <summary>返回键那条历史动作：先存再放行。</summary>
        private async Task LeavePageSaveAsync()
        {
            NavigateController.RemoveHistoryAction(LeavePageSaveAsync);
            await SaveIfDirtyAsync();
        }

        private async Task SaveAsync(bool navigateBack, bool showLoading = true)
        {
            if (showLoading)
            {
                AlertService.StartLoading();
            }

            try
            {
                review.Date = selectedDate;
                review.SetAffirmationIds(selectedAffirmationIds);

                if (!await DailyReviewService.SaveAsync(review, sections))
                {
                    await AlertService.ErrorAsync(I18n.T("Change failed"));
                    return;
                }

                dirty = false;

                if (navigateBack)
                {
                    await NavigateToBack();
                }
            }
            finally
            {
                if (showLoading)
                {
                    AlertService.StopLoading();
                }
            }
        }
    }
}
