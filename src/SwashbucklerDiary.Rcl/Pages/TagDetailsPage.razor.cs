using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    public partial class TagDetailsPage : DiariesPageComponentBase
    {
        private string? tagName;

        private bool isPerson;

        private List<LessonView> lessons = [];

        private List<LessonTypeCount> typeCounts = [];

        private string? currentBoundary;

        private int dueCount;

        private List<DailyEventView> dailyEvents = [];

        [Inject]
        private ILessonService LessonService { get; set; } = default!;

        [Inject]
        private IDailyEventService DailyEventService { get; set; } = default!;

        private readonly string scrollContainerId = $"scroll-container-{Guid.NewGuid():N}";

        private string scrollContainerSelector = string.Empty;

        [Parameter]
        public Guid Id { get; set; }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            scrollContainerSelector = $"#{scrollContainerId}";
        }

        protected override async Task UpdateDiariesAsync()
        {
            var tag = await TagService.FindIncludesAsync(Id);
            if (tag is null)
            {
                await NavigateToBack();
                return;
            }

            tagName = tag?.Name;
            Diaries = tag?.Diaries ?? [];

            isPerson = tag?.Kind == TagKind.Person;
            if (!isPerson)
            {
                lessons = [];
                typeCounts = [];
                currentBoundary = null;
                dueCount = 0;
                dailyEvents = [];
                return;
            }

            //人物页首屏：类型计数、当前边界、待回访；下面是课题与日课片段
            lessons = await LessonService.QueryAsync(personTagId: Id);
            typeCounts = await LessonService.GetTypeCountsAsync(Id);
            currentBoundary = (await LessonService.GetLatestBoundaryByPersonAsync(Id))?.Lesson.Boundary;
            var now = DateTime.Now;
            dueCount = lessons.Count(it =>
                it.Lesson.FollowUpStatus is null
                && it.Lesson.FollowUpDueTime is DateTime due
                && due <= now);
            dailyEvents = await DailyEventService.QueryByPersonAsync(Id);
        }

        private void ToWrite()
        {
            NavigationManager.NavigateTo($"write?tagId={Id}");
        }

        private void OpenLesson(LessonView view)
        {
            To($"lesson?Id={view.Lesson.Id}");
        }

        private void OpenDailyReview(DailyEventView item)
        {
            To($"dailyReview?Date={item.Date:yyyy-MM-dd}");
        }

        private string LessonTitle(LessonView view)
        {
            string date = view.Lesson.OccurredTime.ToString("yyyy/MM/dd");
            return string.IsNullOrWhiteSpace(view.Lesson.Event)
                ? date
                : $"{date} · {view.Lesson.Event.Trim()}";
        }

        private string TypeCountText()
        {
            return string.Join(" · ", typeCounts.Select(it => $"{I18n.T(it.Type.ToString())} {it.Count}"));
        }
    }
}
