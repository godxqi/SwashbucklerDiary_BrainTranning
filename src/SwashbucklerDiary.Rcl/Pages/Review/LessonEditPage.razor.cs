using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 课题记录的结构化表单。照状态管理表的写法：一节一格、选项用芯片、正文只当可读副本。
    /// <para>
    /// 它仍然落成一篇普通 <see cref="DiaryModel"/>，挂「人物」和「课题」两个标签——
    /// 所以库表、日历、搜索、导出、隐私模式全都自动继承，不新建表。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 「课题」这个说法借自阿德勒的「人生课题」（工作 / 交友 / 爱）与「课题分离」，
    /// 经岸见一郎《被讨厌的勇气》通俗化；「每个人都是来给你上课的」是新时代与佛教缘起的
    /// 流行说法（Caroline Myss《神圣契约》, 2001；Brian Weiss《前世今生》, 1988），
    /// 属于意义框架，不是心理学证据。本页只把它当自省工具：
    /// 记录在某个人的某件事上学到了什么、有什么感悟；课题类型放在最后，
    /// 作为「这段相遇让我完成了什么课题」的总结，而不是入场标签。
    /// </remarks>
    public partial class LessonEditPage : ImportantComponentBase
    {
        /// <summary>
        /// 课题类型。和八大情绪一样走枚举：加一个值只改 <see cref="LessonType"/>、
        /// 图标 json 和 i18n（key 就是枚举名），页面这里不用动。
        /// </summary>
        private static readonly LessonType[] lessonTypes = Enum.GetValues<LessonType>();

        /// <summary>正文 4.1 ~ 4.8 的章节标题，顺序与表单一致：类型作为总结放在最后。</summary>
        private static readonly string[] sectionTitleKeys =
        [
            "Person",
            "What happened",
            "My reaction",
            "What it mirrors",
            "Boundary",
            "One line to keep",
            "Next step",
            "Lesson type",
        ];

        /// <summary>四层反应的题干，顺序与 <see cref="LessonSections.Reactions"/> 一致。</summary>
        private static readonly string[] reactionLabelKeys =
        [
            "Physical",
            "Mental",
            "Emotional",
            "Spiritual",
        ];

        private Guid diaryId = Guid.Empty;

        private DateTime occurredTime = DateTime.Now;

        private Guid? personTagId;

        private string personName = string.Empty;

        private HashSet<LessonType> selectedTypes = [];

        /// <summary>正文里认得出来、但不在固定八类里的自定义类型。切换语言后别把它们弄丢。</summary>
        private string lessonTypesExtra = string.Empty;

        private string eventText = string.Empty;

        private string reactionBody = string.Empty;

        private string reactionMind = string.Empty;

        private string reactionEmotion = string.Empty;

        private string reactionSpirit = string.Empty;

        private string mirror = string.Empty;

        private string boundary = string.Empty;

        private string takeaway = string.Empty;

        private string nextStep = string.Empty;

        private List<TagModel> personTags = [];

        private bool showAddPerson;

        private bool showDatePicker;

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
                dirty = true;
            }
        }

        [Inject]
        private IDiaryService DiaryService { get; set; } = default!;

        [Inject]
        private ITagService TagService { get; set; } = default!;

        [Inject]
        private IGlobalConfiguration GlobalConfiguration { get; set; } = default!;

        [SupplyParameterFromQuery]
        private Guid? DiaryId { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            //用返回键离开时也要落地，否则「填了一半按返回」等于白填
            NavigateController.AddHistoryAction(LeavePageSaveAsync, false);
        }

        protected override async Task OnInitializedAsync()
        {
            diaryId = DiaryId ?? Guid.Empty;

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

            if (diaryId == Guid.Empty)
            {
                ResetFields();
                dirty = false;
                await InvokeAsync(StateHasChanged);
                return;
            }

            var diary = await DiaryService.FindAsync(diaryId);
            if (diary is null)
            {
                //那条被删了：退回一张空白表，不让页面停在半空
                diaryId = Guid.Empty;
                ResetFields();
                dirty = false;
                await InvokeAsync(StateHasChanged);
                return;
            }

            var sections = LessonSections.Parse(diary.Content);
            occurredTime = diary.CreateTime;
            personName = sections.Person;
            selectedTypes = ParseTypes(sections.LessonTypes);
            eventText = sections.Event;
            reactionBody = sections.Reactions.ElementAtOrDefault(0) ?? string.Empty;
            reactionMind = sections.Reactions.ElementAtOrDefault(1) ?? string.Empty;
            reactionEmotion = sections.Reactions.ElementAtOrDefault(2) ?? string.Empty;
            reactionSpirit = sections.Reactions.ElementAtOrDefault(3) ?? string.Empty;
            mirror = sections.Mirror;
            boundary = sections.Boundary;
            takeaway = sections.Takeaway;
            nextStep = sections.NextStep;

            var personTag = diary.Tags?.FirstOrDefault(it => it.Kind == TagKind.Person)
                ?? diary.Tags?.FirstOrDefault(it => !string.IsNullOrEmpty(personName)
                    && it.Name == personName
                    && (it.Kind == TagKind.Normal || it.Kind == null))
                ?? tags.FirstOrDefault(it => it.Kind == TagKind.Person && it.Name == personName);
            personTagId = personTag?.Id;

            //旧数据里人物可能只是一个普通标签；这里顺手升级成 Person，下次才会出现在人物芯片里。
            if (personTag is not null && personTag.Kind != TagKind.Person)
            {
                personTag.Kind = TagKind.Person;
                personTag.UpdateTime = DateTime.Now;
                await TagService.UpdateAsync(personTag, it => new { it.Kind, it.UpdateTime });
                if (!personTags.Any(it => it.Id == personTag.Id))
                {
                    personTags.Add(personTag);
                }
            }

            dirty = false;
            await InvokeAsync(StateHasChanged);
        }

        private void ResetFields()
        {
            occurredTime = DateTime.Now;
            personTagId = null;
            personName = string.Empty;
            selectedTypes.Clear();
            lessonTypesExtra = string.Empty;
            eventText = string.Empty;
            reactionBody = string.Empty;
            reactionMind = string.Empty;
            reactionEmotion = string.Empty;
            reactionSpirit = string.Empty;
            mirror = string.Empty;
            boundary = string.Empty;
            takeaway = string.Empty;
            nextStep = string.Empty;
        }

        private HashSet<LessonType> ParseTypes(string? value)
        {
            HashSet<LessonType> result = [];
            lessonTypesExtra = string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            List<string> unmatched = [];
            var names = value.Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string name in names)
            {
                // 0 不是合法的 LessonType，拿它当「没找到」的哨兵值
                LessonType found = Array.Find(lessonTypes, it => I18n.T(it.ToString()) == name);
                if (found != default)
                {
                    result.Add(found);
                }
                else
                {
                    unmatched.Add(name);
                }
            }

            lessonTypesExtra = string.Join("、", unmatched);
            return result;
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

        private void ToggleType(LessonType type)
        {
            if (!selectedTypes.Remove(type))
            {
                selectedTypes.Add(type);
            }

            dirty = true;
        }

        private async Task AddPersonAsync(string? name)
        {
            showAddPerson = false;

            name = name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            var tags = await TagService.QueryAsync();
            var tag = tags.FirstOrDefault(it => it.Name == name
                && (it.Kind == TagKind.Person || it.Kind == TagKind.Normal || it.Kind == null));

            if (tag is null)
            {
                tag = new TagModel
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Kind = TagKind.Person,
                };
                if (!await TagService.AddAsync(tag))
                {
                    await AlertService.ErrorAsync(I18n.T("Add failed"));
                    return;
                }
            }
            else if (tag.Kind != TagKind.Person)
            {
                //同名的旧普通标签直接升级，不另造一个「张三」，否则历史会被劈成两半
                tag.Kind = TagKind.Person;
                tag.UpdateTime = DateTime.Now;
                await TagService.UpdateAsync(tag, it => new { it.Kind, it.UpdateTime });
            }

            personTags = tags.Where(it => it.Kind == TagKind.Person).ToList();
            if (!personTags.Any(it => it.Id == tag.Id))
            {
                personTags.Insert(0, tag);
            }

            personTagId = tag.Id;
            personName = tag.Name ?? name;
            dirty = true;

            await InvokeAsync(StateHasChanged);
        }

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
            if (saving)
            {
                return;
            }

            saving = true;
            if (showLoading)
            {
                AlertService.StartLoading();
            }

            try
            {
                List<string> typeNames = [.. lessonTypes
                    .Where(selectedTypes.Contains)
                    .Select(it => I18n.T(it.ToString()))];
                if (!string.IsNullOrWhiteSpace(lessonTypesExtra))
                {
                    typeNames.Add(lessonTypesExtra);
                }

                var sections = new LessonSections
                {
                    Person = personName,
                    LessonTypes = string.Join("、", typeNames),
                    Event = eventText,
                    Reactions = [reactionBody, reactionMind, reactionEmotion, reactionSpirit],
                    Mirror = mirror,
                    Boundary = boundary,
                    Takeaway = takeaway,
                    NextStep = nextStep,
                };

                if (sections.IsEmpty && personTagId is null)
                {
                    if (navigateBack)
                    {
                        await AlertService.WarningAsync(I18n.T("Nothing to save yet"));
                    }

                    return;
                }

                DiaryModel diary;
                if (diaryId == Guid.Empty)
                {
                    //自己生成 Id，不依赖 InsertAsync 回填（和日课建日记时同一个理由）
                    diary = new DiaryModel
                    {
                        Id = Guid.NewGuid(),
                        Tags = [],
                        Resources = [],
                    };
                }
                else
                {
                    var existing = await DiaryService.FindAsync(diaryId);
                    if (existing is null)
                    {
                        diaryId = Guid.Empty;
                        diary = new DiaryModel
                        {
                            Id = Guid.NewGuid(),
                            Tags = [],
                            Resources = [],
                        };
                    }
                    else
                    {
                        diary = existing;
                    }
                }

                diary.Title = string.IsNullOrWhiteSpace(personName)
                    ? I18n.T("Lesson record")
                    : $"{I18n.T("Lesson record")} · {personName}";
                diary.Content = sections.Compose(
                    sectionTitleKeys.Select(it => I18n.T(it)).ToArray(),
                    reactionLabelKeys.Select(it => I18n.T(it)).ToArray());
                diary.CreateTime = occurredTime;
                diary.UpdateTime = DateTime.Now;

                List<TagModel> diaryTags = [];
                if (personTagId is Guid personId)
                {
                    var person = await TagService.FindAsync(personId);
                    if (person is not null)
                    {
                        diaryTags.Add(person);
                    }
                }

                var lessonTag = await GetOrCreateLessonTagAsync();
                if (lessonTag is not null)
                {
                    diaryTags.Add(lessonTag);
                }

                diary.Tags = diaryTags;

                bool isNew = diaryId == Guid.Empty;
                bool success = isNew
                    ? await DiaryService.AddAsync(diary)
                    : await DiaryService.UpdateIncludesAsync(diary);
                if (!success)
                {
                    await AlertService.ErrorAsync(I18n.T(isNew ? "Add failed" : "Change failed"));
                    return;
                }

                diaryId = diary.Id;
                dirty = false;

                if (navigateBack)
                {
                    await NavigateToBack();
                }
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

        private async Task<TagModel?> GetOrCreateLessonTagAsync()
        {
            var tags = await TagService.QueryAsync();
            var tag = tags.FirstOrDefault(it => it.Kind == TagKind.Lesson)
                ?? tags.FirstOrDefault(it => it.Name == I18n.T("Lesson")
                    && (it.Kind == TagKind.Normal || it.Kind == null));
            if (tag is not null)
            {
                return tag;
            }

            tag = new TagModel
            {
                Id = Guid.NewGuid(),
                Name = I18n.T("Lesson"),
                Kind = TagKind.Lesson,
            };
            if (!await TagService.AddAsync(tag))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return null;
            }

            return tag;
        }
    }

    /// <summary>课题表单各节的「这要填什么」。</summary>
    internal static class LessonText
    {
        public const string PersonDescription =
            "Who this is about. Pick someone, or add a new person.";

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
    }
}
