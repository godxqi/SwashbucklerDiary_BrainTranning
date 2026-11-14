using SqlSugar;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class DailyEventService : IDailyEventService
    {
        private readonly IReviewRepository<DailyEventModel> _eventRepository;

        private readonly IReviewRepository<DailyReviewModel> _dailyReviewRepository;

        public DailyEventService(IReviewRepository<DailyEventModel> eventRepository,
            IReviewRepository<DailyReviewModel> dailyReviewRepository)
        {
            _eventRepository = eventRepository;
            _dailyReviewRepository = dailyReviewRepository;
        }

        public async Task<List<DailyEventModel>> QueryByReviewAsync(Guid dailyReviewId)
        {
            return await _eventRepository.Queryable()
                .Where(it => it.DailyReviewId == dailyReviewId)
                .OrderBy(it => it.OrderIndex)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<bool> SaveAsync(Guid dailyReviewId, IReadOnlyList<DailyEventModel> events)
        {
            var now = DateTime.Now;
            var rows = events
                .Where(it => !string.IsNullOrWhiteSpace(it.Content))
                .Select((it, index) => new DailyEventModel
                {
                    Id = Guid.NewGuid(),
                    DailyReviewId = dailyReviewId,
                    PersonTagId = it.PersonTagId,
                    Content = it.Content!.Trim(),
                    OrderIndex = index,
                    CreateTime = now,
                    UpdateTime = now,
                })
                .ToList();

            return await _eventRepository.UseTranAsync(async context =>
            {
                await context.Deleteable<DailyEventModel>()
                    .Where(it => it.DailyReviewId == dailyReviewId)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                if (rows.Count > 0)
                {
                    await context.Insertable(rows).ExecuteCommandAsync().ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        public Task<bool> DeleteByReviewAsync(Guid dailyReviewId)
        {
            return _eventRepository.DeleteAsync(it => it.DailyReviewId == dailyReviewId);
        }

        public async Task<List<DailyEventView>> QueryByPersonAsync(Guid personTagId)
        {
            var events = await _eventRepository.Queryable()
                .Where(it => it.PersonTagId == personTagId)
                .ToListAsync()
                .ConfigureAwait(false);

            if (events.Count == 0)
            {
                return [];
            }

            var reviewIds = events
                .Select(it => it.DailyReviewId)
                .Distinct()
                .ToList();

            var reviewDates = await _dailyReviewRepository.Queryable()
                .Where(it => reviewIds.Contains(it.Id))
                .Select(it => new { it.Id, it.Date })
                .ToListAsync()
                .ConfigureAwait(false);
            var dateById = reviewDates.ToDictionary(it => it.Id, it => it.Date);

            return [.. events
                .OrderByDescending(it => dateById.GetValueOrDefault(it.DailyReviewId))
                .ThenBy(it => it.OrderIndex)
                .Select(it => new DailyEventView(it, dateById.GetValueOrDefault(it.DailyReviewId)))];
        }
    }
}
