using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 课题列表：按人物、类型、待回访筛选。课题不再进日记流，这里是它的主入口。
    /// </summary>
    public partial class LessonListPage : ImportantComponentBase
    {
        private static readonly LessonType[] lessonTypes = Enum.GetValues<LessonType>();

        private List<LessonView> lessons = [];

        private List<TagModel> personTags = [];

        private Guid? selectedPersonId;

        private LessonType? selectedType;

        private bool dueOnly;

        [Inject]
        private ILessonService LessonService { get; set; } = default!;

        [Inject]
        private ITagService TagService { get; set; } = default!;

        [Inject]
        private IGlobalConfiguration GlobalConfiguration { get; set; } = default!;

        [SupplyParameterFromQuery]
        private bool? Due { get; set; }

        protected override async Task OnInitializedAsync()
        {
            dueOnly = Due == true;

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
            var tags = await TagService.QueryAsync();
            personTags = tags.Where(it => it.Kind == TagKind.Person).ToList();
            lessons = await LessonService.QueryAsync(
                personTagId: selectedPersonId,
                type: selectedType,
                dueOnly: dueOnly);

            await InvokeAsync(StateHasChanged);
        }

        private async Task TogglePerson(Guid id)
        {
            selectedPersonId = selectedPersonId == id ? null : id;
            await LoadAsync();
        }

        private async Task ToggleType(LessonType type)
        {
            selectedType = selectedType == type ? null : type;
            await LoadAsync();
        }

        private async Task ToggleDue()
        {
            dueOnly = !dueOnly;
            await LoadAsync();
        }

        private void Open(LessonView view)
        {
            To($"lesson?Id={view.Lesson.Id}");
        }

        private string Subtitle(LessonView view)
        {
            List<string> parts = [view.Lesson.OccurredTime.ToString("yyyy/MM/dd")];

            if (!string.IsNullOrWhiteSpace(view.Lesson.Event))
            {
                parts.Add(view.Lesson.Event.Trim());
            }

            if (view.Types.Count > 0)
            {
                parts.Add(string.Join("、", view.Types.Select(it => I18n.T(it.ToString()))));
            }

            if (view.Lesson.FollowUpStatus is LessonFollowUpStatus status)
            {
                parts.Add(I18n.T(status.ToString()));
            }
            else if (view.Lesson.FollowUpDueTime is DateTime due)
            {
                parts.Add($"{I18n.T("Follow-up due")} {due:yyyy/MM/dd}");
            }

            return string.Join(" · ", parts);
        }
    }
}
