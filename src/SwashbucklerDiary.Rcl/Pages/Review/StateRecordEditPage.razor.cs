using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 状态管理表（方法文档 5）的完整表单。按需求 7 的口径<b>一次填完</b>，不做折叠。
    /// <para>
    /// 它有两种进入方式：新建一条，以及<b>补全</b>一条半成品——触因页的「＋ 记一次」
    /// 会插一条只填了时间与触因的行，用户之后可以从列表点进来把剩下的格子填上。
    /// </para>
    /// <para>
    /// 它不写日记正文：这张表跟日课/周课/月课不是一条线，没有对应的一篇 <c>DiaryModel</c>。
    /// 内容全在 <see cref="StateRecordModel"/> 自己的列里。
    /// </para>
    /// </summary>
    public partial class StateRecordEditPage : ImportantComponentBase
    {
        private static readonly MoodKind[] moods = Enum.GetValues<MoodKind>();

        private Guid recordId = Guid.Empty;

        private DateTime occurredTime = DateTime.Now;

        private string scene = string.Empty;

        private string body = string.Empty;

        private string mind = string.Empty;

        private string emotion = string.Empty;

        private string spirit = string.Empty;

        private string actionNow = string.Empty;

        private Guid? triggerTagId;

        private MoodKind? emotionName;

        private InvokedKind? invokedKind;

        private Guid? invokedId;

        /// <summary>「调用哪一条」解析出来的正文。它只用来显示，落库的是 Kind + Id。</summary>
        private string? invokedText;

        private List<TriggerCount> triggers = [];

        private bool showAddTrigger;

        private bool showInvokedPicker;

        private bool showDatePicker;

        private bool dirty;

        /// <summary>日历的可选上界：这张表补的是过去，写不了未来。</summary>
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        /// <summary>
        /// 日历绑的是<b>日期</b>，时刻保留原值——补记的粒度是「哪一天」。
        /// </summary>
        private DateOnly SelectedDate
        {
            get => DateOnly.FromDateTime(occurredTime);
            set
            {
                if (value == default)
                {
                    return;
                }

                occurredTime = value.ToDateTime(TimeOnly.FromTimeSpan(occurredTime.TimeOfDay));
                dirty = true;
            }
        }

        [Inject]
        private IStateRecordService StateRecordService { get; set; } = default!;

        [Inject]
        private ITriggerService TriggerService { get; set; } = default!;

        [Inject]
        private IGlobalConfiguration GlobalConfiguration { get; set; } = default!;

        [SupplyParameterFromQuery]
        private Guid? Id { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //用返回键离开时也要落地，否则「填了一半按返回」等于白填
            NavigateController.AddHistoryAction(LeavePageSaveAsync, false);
        }

        protected override async Task OnInitializedAsync()
        {
            recordId = Id ?? Guid.Empty;

            await LoadAsync();

            await base.OnInitializedAsync();
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
            triggers = await TriggerService.GetWithCountsAsync();

            //取不到就退回一张空白表，两条路并成一条：
            // ① 新建（recordId 是空的）；
            // ② 那条已经被删了 —— 比如刚在列表里删掉又按了返回，不该让页面停在一片空白上。
            var view = recordId == Guid.Empty
                ? null
                : await StateRecordService.GetAsync(recordId).ConfigureAwait(false);

            if (view is null)
            {
                recordId = Guid.Empty;
                //新建：发生时间就是「开始记的这一刻」
                occurredTime = DateTime.Now;
                ResetFields();
                dirty = false;

                await InvokeAsync(StateHasChanged);
                return;
            }

            var record = view.Record;
            occurredTime = record.OccurredTime;
            scene = record.Scene ?? string.Empty;
            body = record.Body ?? string.Empty;
            mind = record.Mind ?? string.Empty;
            emotion = record.Emotion ?? string.Empty;
            spirit = record.Spirit ?? string.Empty;
            actionNow = record.ActionNow ?? string.Empty;
            triggerTagId = record.TriggerTagId;
            emotionName = record.EmotionName;

            //指向的那条被删掉时，引用已经悬空了——显示成「还没选」才是诚实的，
            //所以这里把 Kind / Id 一并清掉，而不是留一个指向空处的 Id。
            bool resolvable = view.InvokedText is not null
                && record.InvokedKind is not null
                && record.InvokedId is not null;
            invokedKind = resolvable ? record.InvokedKind : null;
            invokedId = resolvable ? record.InvokedId : null;
            invokedText = resolvable ? view.InvokedText : null;

            dirty = false;

            await InvokeAsync(StateHasChanged);
        }

        private void ResetFields()
        {
            scene = string.Empty;
            body = string.Empty;
            mind = string.Empty;
            emotion = string.Empty;
            spirit = string.Empty;
            actionNow = string.Empty;
            triggerTagId = null;
            emotionName = null;
            invokedKind = null;
            invokedId = null;
            invokedText = null;
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void SelectTrigger(Guid id)
        {
            triggerTagId = triggerTagId == id ? null : id;
            dirty = true;
        }

        private void SelectEmotionName(MoodKind mood)
        {
            emotionName = emotionName == mood ? null : mood;
            dirty = true;
        }

        private void SelectInvoked(InvokedReference reference)
        {
            invokedKind = reference.Kind;
            invokedId = reference.Id;
            invokedText = reference.Text;
            dirty = true;
            showInvokedPicker = false;
        }

        private void ClearInvoked()
        {
            invokedKind = null;
            invokedId = null;
            invokedText = null;
            dirty = true;
            showInvokedPicker = false;
        }

        private async Task AddTriggerAsync(string name)
        {
            showAddTrigger = false;

            string value = name?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return;
            }

            if (!await TriggerService.AddAsync(value))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            triggers = await TriggerService.GetWithCountsAsync();

            //新建完顺手选上——「刚刚就是因为这个才记的」，不该再让人去芯片堆里找一遍
            var added = triggers.FirstOrDefault(it => it.Tag.Name == value);
            if (added is not null)
            {
                triggerTagId = added.Tag.Id;
                dirty = true;
            }

            await InvokeAsync(StateHasChanged);
        }

        /// <summary>换页面 / 离开之前，先把改动落地。</summary>
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
                var record = new StateRecordModel
                {
                    Id = recordId,
                    OccurredTime = occurredTime,
                    Scene = Normalize(scene),
                    TriggerTagId = triggerTagId,
                    Body = Normalize(body),
                    Mind = Normalize(mind),
                    Emotion = Normalize(emotion),
                    Spirit = Normalize(spirit),
                    EmotionName = emotionName,
                    InvokedKind = invokedKind,
                    InvokedId = invokedId,
                    ActionNow = Normalize(actionNow),
                };

                var savedId = await StateRecordService.SaveAsync(record);
                if (savedId is null)
                {
                    //整张表一个字都没填。安静地当成功会让人以为记下来了
                    await AlertService.WarningAsync(I18n.T("Nothing to save yet"));
                    return;
                }

                recordId = savedId.Value;
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

        /// <summary>全是空白就存成 null——空字符串与「没填」在库里该是同一个意思。</summary>
        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }

    /// <summary>状态管理表各格的「这要填什么」，取自方法文档 5.1 的正文。</summary>
    internal static class StateRecordText
    {
        public const string Intro =
            "The point of this table: when you are in a bad state, write down what you feel and what you need to do to move yourself back toward your source. It is not part of the daily rhythm - record it whenever it happens.";

        public const string SceneDescription = "What is going on right now.";

        public const string OccurredTimeDescription =
            "It defaults to now. Backfilling something from before? Pick the day it actually happened.";

        public const string QuickEntrySubtitle =
            "Write down what you feel and what you need to do to move back toward your source.";

        /// <summary>「现在要调用哪一条」那一格的说明。原先单独放在 InvokedPickerText 里，归到这张表的文案里。</summary>
        public const string CallOnDescription =
            "Which entry you actually turned to (a virtue table, the affirmation library, or a macro goal). This is the most important cell in the whole table: these tables were built for exactly this moment. The book's instruction: once a limiting belief resurfaces, remind yourself that you have already weakened its influence. If nothing is called on here, they are just a collection.";

        public const string ListDescription =
            "Every state record, newest first. An entry marked \"to complete\" came from the quick log on the trigger page - open it and fill in the rest.";

        public const string TriggerDescription =
            "The trigger: the specific thing, scene, or person in the moment that feels familiar, and the clue it gives you about which past situation it resembles. Triggers are not random - the same kind turning up three times or more is the fingerprint of a limiting belief, and it goes to the monthly review 3.1 (see 5.2).";

        public const string BodyDescription = "Physical sensations.";

        public const string MindDescription = "What you are thinking.";

        public const string EmotionDescription = "What you feel.";

        public const string SpiritDescription =
            "Deep sense of meaning, purpose, and your place in the world.";

        public const string EmotionNameDescription =
            "Emotional literacy. Naming it keeps some objectivity in play, which makes it far less likely to sweep you away. Pick one of the eight.";

        public const string ActionNowDescription =
            "The lowest-barrier action that moves you from a bad state back toward your source: sensory pleasure, the STOP exercise, or the fallback - going to bed.";
    }
}
