using SqlSugar;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class LessonService : ILessonService
    {
        private readonly IReviewRepository<LessonModel> _lessonRepository;

        private readonly IReviewRepository<LessonTypeLinkModel> _typeRepository;

        private readonly ITagService _tagService;

        public LessonService(IReviewRepository<LessonModel> lessonRepository,
            IReviewRepository<LessonTypeLinkModel> typeRepository,
            ITagService tagService)
        {
            _lessonRepository = lessonRepository;
            _typeRepository = typeRepository;
            _tagService = tagService;
        }

        public async Task<List<LessonView>> QueryAsync(
            Guid? personTagId = null,
            LessonType? type = null,
            bool dueOnly = false,
            DateTime? now = null)
        {
            var query = _lessonRepository.Queryable();

            if (personTagId is Guid personId)
            {
                query = query.Where(it => it.PersonTagId == personId);
            }

            if (dueOnly)
            {
                var deadline = now ?? DateTime.Now;
                query = query
                    .Where(it => it.FollowUpStatus == null)
                    .Where(it => it.FollowUpDueTime != null)
                    .Where(it => it.FollowUpDueTime <= deadline);
            }

            if (type is LessonType lessonType)
            {
                var lessonIds = await _typeRepository.Queryable()
                    .Where(it => it.Type == lessonType)
                    .Select(it => it.LessonId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                query = query.Where(it => lessonIds.Contains(it.Id));
            }

            var lessons = await query
                .OrderByDescending(it => it.OccurredTime)
                .ToListAsync()
                .ConfigureAwait(false);

            return await ToViewsAsync(lessons).ConfigureAwait(false);
        }

        public async Task<LessonView?> GetAsync(Guid id)
        {
            var lesson = await _lessonRepository.GetByIdAsync(id).ConfigureAwait(false);
            if (lesson is null)
            {
                return null;
            }

            var views = await ToViewsAsync([lesson]).ConfigureAwait(false);
            return views.Count == 0 ? null : views[0];
        }

        public async Task<List<LessonView>> SearchAsync(string keyword)
        {
            string value = keyword?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return [];
            }

            //课题表不大，先在 C# 里筛文本；人物名要先进标签表换成 Id
            var personTags = await _tagService
                .QueryAsync(it => it.Name != null && it.Name.Contains(value))
                .ConfigureAwait(false);
            var personIds = personTags.Select(it => it.Id).ToList();

            var lessons = await _lessonRepository.Queryable()
                .OrderByDescending(it => it.OccurredTime)
                .ToListAsync()
                .ConfigureAwait(false);

            var matches = lessons
                .Where(it => (it.PersonTagId is Guid personId && personIds.Contains(personId))
                    || Contains(it.Event)
                    || Contains(it.Mirror)
                    || Contains(it.Boundary)
                    || Contains(it.Takeaway)
                    || Contains(it.NextStep))
                .ToList();

            return await ToViewsAsync(matches).ConfigureAwait(false);

            bool Contains(string? text)
                => text?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
        }

        public async Task<Guid?> SaveAsync(LessonModel lesson, IReadOnlyCollection<LessonType> types)
        {
            var now = DateTime.Now;
            if (lesson.OccurredTime == default)
            {
                lesson.OccurredTime = now;
            }

            lesson.FollowUpDueTime ??= lesson.OccurredTime.Date.AddDays(14);
            lesson.UpdateTime = now;

            bool isNew = lesson.Id == Guid.Empty;
            if (isNew)
            {
                lesson.Id = Guid.NewGuid();
                lesson.CreateTime = now;
            }
            else
            {
                var existing = await _lessonRepository.GetByIdAsync(lesson.Id).ConfigureAwait(false);
                if (existing is null)
                {
                    return null;
                }

                lesson.CreateTime = existing.CreateTime;
            }

            var distinctTypes = types.Distinct().ToList();

            bool success = await _lessonRepository.UseTranAsync(async context =>
            {
                if (isNew)
                {
                    await context.Insertable(lesson).ExecuteCommandAsync().ConfigureAwait(false);
                }
                else
                {
                    await context.Updateable(lesson).ExecuteCommandAsync().ConfigureAwait(false);
                }

                await context.Deleteable<LessonTypeLinkModel>()
                    .Where(it => it.LessonId == lesson.Id)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                if (distinctTypes.Count > 0)
                {
                    var rows = distinctTypes.Select(type => new LessonTypeLinkModel
                    {
                        Id = Guid.NewGuid(),
                        LessonId = lesson.Id,
                        Type = type,
                        CreateTime = now,
                        UpdateTime = now,
                    }).ToList();

                    await context.Insertable(rows).ExecuteCommandAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);

            return success ? lesson.Id : null;
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return _lessonRepository.UseTranAsync(async context =>
            {
                await context.Deleteable<LessonTypeLinkModel>()
                    .Where(it => it.LessonId == id)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await context.Deleteable<LessonModel>()
                    .Where(it => it.Id == id)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                //归档行只留文字，但来源 Id 不能悬空：课题删了就一起摘掉
                await context.Updateable<ArchiveItemModel>()
                    .SetColumns(it => it.SourceLessonId == null)
                    .Where(it => it.SourceLessonId == id)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await context.Updateable<VirtueEntryModel>()
                    .SetColumns(it => it.SourceLessonId == null)
                    .Where(it => it.SourceLessonId == id)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await context.Updateable<SelfAffirmationModel>()
                    .SetColumns(it => it.SourceLessonId == null)
                    .Where(it => it.SourceLessonId == id)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);
            });
        }

        public async Task<List<LessonTypeCount>> GetTypeCountsAsync(Guid? personTagId = null)
        {
            var lessonIds = await GetLessonIdsAsync(personTagId).ConfigureAwait(false);
            if (lessonIds.Count == 0)
            {
                return [];
            }

            var rows = await _typeRepository.Queryable()
                .Where(it => lessonIds.Contains(it.LessonId))
                .GroupBy(it => it.Type)
                .Select(it => new { it.Type, Count = SqlFunc.AggregateCount(it.Id) })
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. rows
                .Select(it => new LessonTypeCount(it.Type, it.Count))
                .OrderByDescending(it => it.Count)
                .ThenBy(it => it.Type)];
        }

        public async Task<LessonView?> GetLatestBoundaryByPersonAsync(Guid personTagId)
        {
            var lesson = await _lessonRepository.Queryable()
                .Where(it => it.PersonTagId == personTagId)
                .Where(it => !SqlFunc.IsNullOrEmpty(it.Boundary))
                .OrderByDescending(it => it.OccurredTime)
                .FirstAsync()
                .ConfigureAwait(false);

            return lesson is null ? null : await GetAsync(lesson.Id).ConfigureAwait(false);
        }

        public Task<List<LessonView>> GetDueFollowUpsAsync(DateTime now)
        {
            return QueryAsync(dueOnly: true, now: now);
        }

        public async Task<List<LessonTypeCount>> GetMonthlyCandidateTypesAsync(
            DateTime month,
            int minCount = 2)
        {
            var (start, end) = MonthRange(month);
            var lessonIds = await _lessonRepository.Queryable()
                .Where(it => it.OccurredTime >= start && it.OccurredTime < end)
                .Select(it => it.Id)
                .ToListAsync()
                .ConfigureAwait(false);
            if (lessonIds.Count == 0)
            {
                return [];
            }

            var rows = await _typeRepository.Queryable()
                .Where(it => lessonIds.Contains(it.LessonId))
                .GroupBy(it => it.Type)
                .Select(it => new { it.Type, Count = SqlFunc.AggregateCount(it.Id) })
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. rows
                .Select(it => new LessonTypeCount(it.Type, it.Count))
                .Where(it => it.Count >= minCount)
                .OrderByDescending(it => it.Count)
                .ThenBy(it => it.Type)];
        }

        public async Task<List<LessonPersonCount>> GetMonthlyCandidatePersonsAsync(
            DateTime month,
            int minCount = 3)
        {
            var (start, end) = MonthRange(month);
            var rows = await _lessonRepository.Queryable()
                .Where(it => it.OccurredTime >= start && it.OccurredTime < end)
                .Where(it => it.PersonTagId != null)
                .GroupBy(it => it.PersonTagId)
                .Select(it => new
                {
                    PersonTagId = it.PersonTagId,
                    Count = SqlFunc.AggregateCount(it.Id),
                })
                .ToListAsync()
                .ConfigureAwait(false);

            var candidates = rows
                .Where(it => it.PersonTagId is not null && it.Count >= minCount)
                .ToList();
            if (candidates.Count == 0)
            {
                return [];
            }

            var ids = candidates.Select(it => it.PersonTagId!.Value).ToList();
            var tags = await _tagService
                .QueryAsync(it => ids.Contains(it.Id))
                .ConfigureAwait(false);
            var names = tags
                .Where(it => !string.IsNullOrWhiteSpace(it.Name))
                .ToDictionary(it => it.Id, it => it.Name!);

            return [.. candidates
                .Select(it => new LessonPersonCount(
                    it.PersonTagId!.Value,
                    names.GetValueOrDefault(it.PersonTagId!.Value),
                    it.Count))
                .OrderByDescending(it => it.Count)];
        }

        private async Task<List<Guid>> GetLessonIdsAsync(Guid? personTagId)
        {
            var query = _lessonRepository.Queryable();
            if (personTagId is Guid personId)
            {
                query = query.Where(it => it.PersonTagId == personId);
            }

            return await query
                .Select(it => it.Id)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        private async Task<List<LessonView>> ToViewsAsync(List<LessonModel> lessons)
        {
            if (lessons.Count == 0)
            {
                return [];
            }

            var lessonIds = lessons.Select(it => it.Id).ToList();

            var links = await _typeRepository.Queryable()
                .Where(it => lessonIds.Contains(it.LessonId))
                .ToListAsync()
                .ConfigureAwait(false);
            var typesByLesson = links
                .GroupBy(it => it.LessonId)
                .ToDictionary(it => it.Key,
                    it => it.Select(link => link.Type).Distinct().OrderBy(type => type).ToList());

            var tagIds = lessons
                .SelectMany(it => new[] { it.PersonTagId, it.TriggerTagId })
                .Where(it => it is not null)
                .Select(it => it!.Value)
                .Distinct()
                .ToList();
            Dictionary<Guid, string> tagNames = [];
            if (tagIds.Count > 0)
            {
                var tags = await _tagService
                    .QueryAsync(it => tagIds.Contains(it.Id))
                    .ConfigureAwait(false);
                tagNames = tags
                    .Where(it => !string.IsNullOrWhiteSpace(it.Name))
                    .ToDictionary(it => it.Id, it => it.Name!);
            }

            return [.. lessons.Select(lesson => new LessonView(
                lesson,
                lesson.PersonTagId is Guid personId ? tagNames.GetValueOrDefault(personId) : null,
                lesson.TriggerTagId is Guid triggerId ? tagNames.GetValueOrDefault(triggerId) : null,
                typesByLesson.GetValueOrDefault(lesson.Id) ?? []))];
        }

        private static (DateTime Start, DateTime End) MonthRange(DateTime month)
        {
            var start = new DateTime(month.Year, month.Month, 1);
            return (start, start.AddMonths(1));
        }
    }
}
