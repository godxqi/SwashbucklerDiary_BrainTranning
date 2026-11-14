using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 课题记录的结构化表单。课题不再是日记：内容写进 <see cref="LessonModel"/>，
    /// 类型写进关联表，人物与触因指向标签。
    /// <para>
    /// 「课题」这个说法借自阿德勒的「人生课题」（工作 / 交友 / 爱）与「课题分离」，
    /// 经岸见一郎《被讨厌的勇气》通俗化；「每个人都是来给你上课的」是新时代与佛教缘起的
    /// 流行说法，属于意义框架，不是心理学证据。本页只把它当自省工具。
    /// </para>
    /// </remarks>
    public partial class LessonEditPage : ImportantComponentBase
    {
        /// <summary>课题类型。加一个值只改枚举、图标 json 与 i18n。</summary>
        private static readonly LessonType[] lessonTypes = Enum.GetValues<LessonType>();

        /// <summary>回访结果。顺序固定：做到 / 部分 / 没做到 / 不再相关。</summary>
        private static readonly LessonFollowUpStatus[] followUpStatuses =
            Enum.GetValues<LessonFollowUpStatus>();

        private Guid lessonId = Guid.Empty;

        private DateTime occurredTime = DateTime.Now;

        private Guid? personTagId;

        private string personName = string.Empty;

        private Guid? triggerTagId;

        private HashSet<LessonType> selectedTypes = [];

        private string eventText = string.Empty;

        private string reactionBody = string.Empty;

        private string reactionMind = string.Empty;

        private string reactionEmotion = string.Empty;

        private string reactionSpirit = string.Empty;

        private string mirror = string.Empty;

        private string boundary = string.Empty;

        private string takeaway = string.Empty;

        private string nextStep = string.Empty;

        private DateTime? followUpDueTime;

        /// <summary>用户是否手动改过回访日期。没改过时，默认跟着发生日走。</summary>
        private bool followUpDueEdited;

        private LessonFollowUpStatus? followUpStatus;

        private string followUpNote = string.Empty;

        /// <summary>归档目标：归档区四栏 + 三个优点表 + 肯定库 + 宏观目标。</summary>
        private static readonly ArchiveTarget[] archiveTargets = Enum.GetValues<ArchiveTarget>();

        private ArchiveTarget archiveTarget = ArchiveTarget.SelfInsight;

        private Guid? macroGoalId;

        private bool archiveSaving;

        private List<TagModel> personTags = [];

        private List<TriggerCount> triggers = [];

        private List<MacroGoalModel> macroGoals = [];

        private bool showAddPerson;

        private bool showAddTrigger;

        private bool showDatePicker;

        private bool showFollowUpDatePicker;

        private bool dirty;

        private bool saving;

        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

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
                if (!followUpDueEdited)
                {
                    followUpDueTime = occurredTime.Date.AddDays(14);
                }

                dirty = true;
            }
        }

        private DateOnly SelectedFollowUpDate
        {
            get => DateOnly.FromDateTime(followUpDueTime ?? occurredTime.Date.AddDays(14));
            set
            {
                if (value == default)
                {
                    return;
                }

                followUpDueTime = value.ToDateTime(TimeOnly.MinValue);
                followUpDueEdited = true;
                dirty = true;
            }
        }

        [Inject]
        private ILessonService LessonService { get; set; } = default!;

        [Inject]
        private ITagService TagService { get; set; } = default!;

        [Inject]
        private ITriggerService TriggerService { get; set; } = default!;

        [Inject]
        private IGlobalConfiguration GlobalConfiguration { get; set; } = default!;

        [Inject]
        private IArchiveService ArchiveService { get; set; } = default!;

        [Inject]
        private IVirtueService VirtueService { get; set; } = default!;

        [Inject]
        private IAffirmationService AffirmationService { get; set; } = default!;

        [Inject]
        private IMacroGoalService MacroGoalService { get; set; } = default!;

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
            lessonId = Id ?? Guid.Empty;

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
            var tags = await TagService.QueryAsync();
            personTags = tags.Where(it => it.Kind == TagKind.Person).ToList();
            triggers = await TriggerService.GetWithCountsAsync();
            macroGoals = await MacroGoalService.GetActiveAsync();

            if (lessonId == Guid.Empty)
            {
                ResetFields();
                dirty = false;
                await InvokeAsync(StateHasChanged);
                return;
            }

            var view = await LessonService.GetAsync(lessonId);
            if (view is null)
            {
                //那条被删了：退回一张空白表，不让页面停在半空
                lessonId = Guid.Empty;
                ResetFields();
                dirty = false;
                await InvokeAsync(StateHasChanged);
                return;
            }

            var lesson = view.Lesson;
            occurredTime = lesson.OccurredTime;
            personTagId = view.PersonName is null ? null : lesson.PersonTagId;
            personName = view.PersonName ?? string.Empty;
            triggerTagId = view.TriggerName is null ? null : lesson.TriggerTagId;
            selectedTypes = [.. view.Types];
            eventText = lesson.Event ?? string.Empty;
            reactionBody = lesson.ReactionBody ?? string.Empty;
            reactionMind = lesson.ReactionMind ?? string.Empty;
            reactionEmotion = lesson.ReactionEmotion ?? string.Empty;
            reactionSpirit = lesson.ReactionSpirit ?? string.Empty;
            mirror = lesson.Mirror ?? string.Empty;
            boundary = lesson.Boundary ?? string.Empty;
            takeaway = lesson.Takeaway ?? string.Empty;
            nextStep = lesson.NextStep ?? string.Empty;
            followUpDueTime = lesson.FollowUpDueTime ?? lesson.OccurredTime.Date.AddDays(14);
            followUpDueEdited = lesson.FollowUpDueTime is not null;
            followUpStatus = lesson.FollowUpStatus;
            followUpNote = lesson.FollowUpNote ?? string.Empty;

            dirty = false;
            await InvokeAsync(StateHasChanged);
        }

        private void ResetFields()
        {
            occurredTime = DateTime.Now;
            personTagId = null;
            personName = string.Empty;
            triggerTagId = null;
            selectedTypes.Clear();
            eventText = string.Empty;
            reactionBody = string.Empty;
            reactionMind = string.Empty;
            reactionEmotion = string.Empty;
            reactionSpirit = string.Empty;
            mirror = string.Empty;
            boundary = string.Empty;
            takeaway = string.Empty;
            nextStep = string.Empty;
            followUpDueTime = occurredTime.Date.AddDays(14);
            followUpDueEdited = false;
            followUpStatus = null;
            followUpNote = string.Empty;
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void SelectPerson(TagModel tag)
        {
            if (personTagId == tag.Id)
            {
                personTagId = null;
                personName = string.Empty;
            }
            else
            {
                personTagId = tag.Id;
                personName = tag.Name ?? string.Empty;
            }

            dirty = true;
        }

        private void SelectTrigger(Guid id)
        {
            triggerTagId = triggerTagId == id ? null : id;
            dirty = true;
        }

        private void ToggleType(LessonType type)
        {
            if (!selectedTypes.Remove(type))
            {
                selectedTypes.Add(type);
            }

            dirty = true;
        }

        private void ToggleFollowUpStatus(LessonFollowUpStatus status)
        {
            followUpStatus = followUpStatus == status ? null : status;
            dirty = true;
        }

        private async Task AddPersonAsync(string? name)
        {
            showAddPerson = false;

            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var tag = await TagService.GetOrCreatePersonAsync(name);
            if (tag is null)
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            personTags = (await TagService.QueryAsync())
                .Where(it => it.Kind == TagKind.Person)
                .ToList();
            if (!personTags.Any(it => it.Id == tag.Id))
            {
                personTags.Insert(0, tag);
            }

            personTagId = tag.Id;
            personName = tag.Name ?? name.Trim();
            dirty = true;

            await InvokeAsync(StateHasChanged);
        }

        private async Task AddTriggerAsync(string? name)
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
            var added = triggers.FirstOrDefault(it => it.Tag.Name == value);
            if (added is not null)
            {
                triggerTagId = added.Tag.Id;
                dirty = true;
            }

            await InvokeAsync(StateHasChanged);
        }

        private async Task SaveIfDirtyAsync()
        {
            if (!dirty)
            {
                return;
            }

            await SaveLessonAsync(false, false);
        }

        /// <summary>返回键那条历史动作：先存再放行。</summary>
        private async Task LeavePageSaveAsync()
        {
            NavigateController.RemoveHistoryAction(LeavePageSaveAsync);
            await SaveIfDirtyAsync();
        }

        private Task SaveAsync(bool navigateBack, bool showLoading = true)
        {
            return SaveLessonAsync(navigateBack, showLoading);
        }

        private async Task<Guid?> SaveLessonAsync(
            bool navigateBack,
            bool showLoading = true,
            bool warnWhenEmpty = true)
        {
            if (saving)
            {
                return null;
            }

            if (IsEmpty())
            {
                if (warnWhenEmpty && navigateBack)
                {
                    await AlertService.WarningAsync(I18n.T("Nothing to save yet"));
                }

                return null;
            }

            saving = true;
            if (showLoading)
            {
                AlertService.StartLoading();
            }

            try
            {
                var lesson = new LessonModel
                {
                    Id = lessonId,
                    OccurredTime = occurredTime,
                    PersonTagId = personTagId,
                    TriggerTagId = triggerTagId,
                    Event = Normalize(eventText),
                    ReactionBody = Normalize(reactionBody),
                    ReactionMind = Normalize(reactionMind),
                    ReactionEmotion = Normalize(reactionEmotion),
                    ReactionSpirit = Normalize(reactionSpirit),
                    Mirror = Normalize(mirror),
                    Boundary = Normalize(boundary),
                    Takeaway = Normalize(takeaway),
                    NextStep = Normalize(nextStep),
                    FollowUpDueTime = followUpDueTime,
                    FollowUpStatus = followUpStatus,
                    FollowUpNote = Normalize(followUpNote),
                };

                bool isNew = lessonId == Guid.Empty;
                var savedId = await LessonService.SaveAsync(lesson, selectedTypes);
                if (savedId is null)
                {
                    await AlertService.ErrorAsync(I18n.T(isNew ? "Add failed" : "Change failed"));
                    return null;
                }

                lessonId = savedId.Value;
                dirty = false;

                if (navigateBack)
                {
                    await NavigateToBack();
                }

                return savedId;
            }
            finally
            {
                saving = false;
                if (showLoading)
                {
                    AlertService.StopLoading();
                }
            }
        }

        private bool IsEmpty()
        {
            return personTagId is null
                && triggerTagId is null
                && selectedTypes.Count == 0
                && string.IsNullOrWhiteSpace(eventText)
                && string.IsNullOrWhiteSpace(reactionBody)
                && string.IsNullOrWhiteSpace(reactionMind)
                && string.IsNullOrWhiteSpace(reactionEmotion)
                && string.IsNullOrWhiteSpace(reactionSpirit)
                && string.IsNullOrWhiteSpace(mirror)
                && string.IsNullOrWhiteSpace(boundary)
                && string.IsNullOrWhiteSpace(takeaway)
                && string.IsNullOrWhiteSpace(nextStep)
                && followUpStatus is null
                && string.IsNullOrWhiteSpace(followUpNote);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private string ArchiveTargetName(ArchiveTarget target) => target switch
        {
            ArchiveTarget.FuturePlan => ReviewSectionText.ArchiveKindText(I18n, ArchiveKind.FuturePlan),
            ArchiveTarget.DailyThought => ReviewSectionText.ArchiveKindText(I18n, ArchiveKind.DailyThought),
            ArchiveTarget.MicroFailure => ReviewSectionText.ArchiveKindText(I18n, ArchiveKind.MicroFailure),
            ArchiveTarget.SelfInsight => ReviewSectionText.ArchiveKindText(I18n, ArchiveKind.SelfInsight),
            ArchiveTarget.Positive => I18n.T("Positive list"),
            ArchiveTarget.Gratitude => I18n.T("Gratitude list"),
            ArchiveTarget.Achievement => I18n.T("Achievement list"),
            ArchiveTarget.Affirmation => I18n.T("Affirmation library"),
            _ => I18n.T("Macro goal"),
        };

        /// <summary>
        /// 把「带走一句」收进选中的那一栏。归档立即落库；还没存过课题时先保存一次，
        /// 这样归档行能记下来自哪篇课题。
        /// <para>
        /// 重复的语义按目标分开：7.4 的重复是「又发生了一次」，要让 ArchiveService 累计次数；
        /// 其余八栏同一条只该有一份，已存在就提示。
        /// </para>
        /// </summary>
        private async Task ArchiveTakeawayAsync()
        {
            string value = takeaway.Trim();
            if (value.Length == 0 || archiveSaving)
            {
                return;
            }

            if (lessonId == Guid.Empty)
            {
                var savedId = await SaveLessonAsync(false, false, true);
                if (savedId is null)
                {
                    return;
                }
            }

            archiveSaving = true;
            try
            {
                bool success;
                int? repeatCount = null;

                switch (archiveTarget)
                {
                    case ArchiveTarget.SelfInsight:
                    case ArchiveTarget.FuturePlan:
                    case ArchiveTarget.DailyThought:
                    case ArchiveTarget.MicroFailure:
                        ArchiveKind archiveKind = archiveTarget switch
                        {
                            ArchiveTarget.FuturePlan => ArchiveKind.FuturePlan,
                            ArchiveTarget.DailyThought => ArchiveKind.DailyThought,
                            ArchiveTarget.MicroFailure => ArchiveKind.MicroFailure,
                            _ => ArchiveKind.SelfInsight,
                        };

                        var archived = (await ArchiveService.QueryAsync(archiveKind))
                            .FirstOrDefault(it => IsSame(it.Content, value));
                        if (archived is not null && archiveKind != ArchiveKind.MicroFailure)
                        {
                            await AlertService.InfoAsync(I18n.T("Already archived"));
                            return;
                        }

                        success = await ArchiveService.ArchiveAsync(
                            archiveKind,
                            ArchiveSource.Manual,
                            value,
                            sourceLessonId: lessonId);
                        if (success && archiveKind == ArchiveKind.MicroFailure)
                        {
                            repeatCount = (archived?.OccurrenceCount ?? 0) + 1;
                        }
                        break;

                    case ArchiveTarget.Positive:
                    case ArchiveTarget.Gratitude:
                    case ArchiveTarget.Achievement:
                        VirtueKind virtueKind = archiveTarget switch
                        {
                            ArchiveTarget.Gratitude => VirtueKind.Gratitude,
                            ArchiveTarget.Achievement => VirtueKind.Achievement,
                            _ => VirtueKind.Positive,
                        };

                        if ((await VirtueService.QueryAsync(virtueKind)).Any(it => IsSame(it.Content, value)))
                        {
                            await AlertService.InfoAsync(I18n.T("Already archived"));
                            return;
                        }

                        success = await VirtueService.AddAsync(
                            virtueKind,
                            value,
                            occurredTime,
                            sourceLessonId: lessonId);
                        break;

                    case ArchiveTarget.Affirmation:
                        if ((await AffirmationService.QueryAsync()).Any(it => IsSame(it.Content, value)))
                        {
                            await AlertService.InfoAsync(I18n.T("Already archived"));
                            return;
                        }

                        success = await AffirmationService.AddAsync(new SelfAffirmationModel
                        {
                            Content = value,
                            SourceLessonId = lessonId,
                        });
                        break;

                    default:
                        var goal = macroGoals.FirstOrDefault(it => it.Id == macroGoalId);
                        if (goal is null)
                        {
                            return;
                        }

                        string note = goal.Description?.Trim() ?? string.Empty;
                        if (IsSameLine(note, value))
                        {
                            await AlertService.InfoAsync(I18n.T("Already archived"));
                            return;
                        }

                        goal.Description = note.Length == 0 ? value : $"{note}\n{value}";
                        success = await MacroGoalService.SaveAsync(goal);
                        break;
                }

                if (!success)
                {
                    await AlertService.ErrorAsync(I18n.T("Add failed"));
                    return;
                }

                string targetName = ArchiveTargetName(archiveTarget);
                await AlertService.SuccessAsync(repeatCount is int count
                    ? I18n.T("Archived to {0} (time {1})", targetName, count)
                    : I18n.T("Archived to {0}", targetName));
            }
            finally
            {
                archiveSaving = false;
            }
        }

        private static bool IsSame(string? content, string value)
            => string.Equals(content?.Trim(), value, StringComparison.Ordinal);

        private static bool IsSameLine(string note, string value)
            => note.Split('\n').Any(it => string.Equals(it.Trim(), value, StringComparison.Ordinal));

        /// <summary>
        /// 「带走一句」能归档到的九栏：归档区 7.1~7.4、三个优点表、自我肯定库、宏观目标。
        /// </summary>
        private enum ArchiveTarget
        {
            SelfInsight,
            FuturePlan,
            DailyThought,
            MicroFailure,
            Positive,
            Gratitude,
            Achievement,
            Affirmation,
            MacroGoal,
        }
    }

    /// <summary>课题表单各节的「这要填什么」。</summary>
    internal static class LessonText
    {
        public const string PersonDescription =
            "Who this is about. Pick someone, or add a new person.";

        public const string TriggerDescription =
            "What this reminded you of. Reuse the trigger tags from the state record; the same trigger counts on both sides.";

        public const string TypeDescription =
            "Write the experience first, then come back: which kind of lesson did this relationship move forward? Pick as many as apply.";

        public const string EventDescription =
            "What actually happened - observable behavior and exact words, not judgments.";

        public const string ReactionDescription =
            "What it stirred in you, layer by layer.";

        public const string MirrorDescription =
            "What it mirrors in you: an old wound, a projection, a limiting belief.";

        public const string BoundaryDescription =
            "What is yours to carry, and what is not.";

        public const string TakeawayDescription =
            "The one line you want to keep.";

        public const string NextStepDescription =
            "One concrete action, not a resolution.";

        public const string FollowUpDescription =
            "Come back when it is due: did the next step happen, and did the relationship change?";
    }
}
