using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class ArchiveService : IArchiveService
    {
        private readonly IReviewRepository<ArchiveItemModel> _archiveRepository;

        private readonly IReviewRepository<StumblingBlockModel> _blockRepository;

        private readonly IReviewScheduleService _scheduleService;

        public ArchiveService(IReviewRepository<ArchiveItemModel> archiveRepository,
            IReviewRepository<StumblingBlockModel> blockRepository,
            IReviewScheduleService scheduleService)
        {
            _archiveRepository = archiveRepository;
            _blockRepository = blockRepository;
            _scheduleService = scheduleService;
        }

        public Task<List<ArchiveItemModel>> QueryAsync(ArchiveKind? kind = null)
        {
            var query = _archiveRepository.Queryable();

            if (kind.HasValue)
            {
                query = query.Where(it => it.Kind == kind.Value);
            }

            return query
                .OrderByDescending(it => it.LastOccurredTime)
                .ToListAsync();
        }

        public async Task<bool> ArchiveAsync(ArchiveKind kind,
            ArchiveSource source,
            string? content,
            Guid? sourceWeeklyReviewId = null,
            Guid? sourceLessonId = null)
        {
            string value = content?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return true;
            }

            //SqlSugar 没有可用的唯一索引配置位（见 AddSqlsugar 的注释），
            //所以「同一条不重复建、只加计数」由这里按业务键保证。
            var existing = await _archiveRepository.Queryable()
                .Where(it => it.Kind == kind && it.Content == value)
                .FirstAsync()
                .ConfigureAwait(false);

            if (existing is not null)
            {
                //次数只有 7.4 有语义（文档 7.4 的出口就是靠它数出「不是偶发」）。
                //别的 List 里重复写同一条只当作「它还在」——7.1 那种地方把次数刷高没有意义，
                //反而会让「这条观念我提过几次」这种误解冒出来。
                if (kind == ArchiveKind.MicroFailure)
                {
                    existing.OccurrenceCount++;
                }

                //原来没有来源、这次从课题来的，就补上，别让来源永远空着
                existing.SourceLessonId ??= sourceLessonId;
                existing.LastOccurredTime = DateTime.Now;
                existing.UpdateTime = DateTime.Now;

                return await _archiveRepository.UpdateAsync(existing)
                    .ConfigureAwait(false);
            }

            return await _archiveRepository.InsertAsync(new ArchiveItemModel
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Source = source,
                Content = value,
                OccurrenceCount = 1,
                LastOccurredTime = DateTime.Now,
                SourceWeeklyReviewId = sourceWeeklyReviewId,
                SourceLessonId = sourceLessonId,
            }).ConfigureAwait(false);
        }

        public Task<List<ArchiveItemModel>> GetFeedbackCandidatesAsync(int minCount = 3)
        {
            return _archiveRepository.Queryable()
                .Where(it => it.Kind == ArchiveKind.MicroFailure)
                .Where(it => it.OccurrenceCount >= minCount)
                .Where(it => it.FedBackToWeekOfYear == null)
                .OrderByDescending(it => it.OccurrenceCount)
                .ToListAsync();
        }

        public async Task<List<string>> GetRecurringContentsAsync(int minCount = 3)
        {
            //按「取回来再在 C# 里筛」写，不用 Select 投影：这套查询形状在项目里久经验证，
            //而 7.4 本来就不大（同一条只占一行）。
            var items = await _archiveRepository.Queryable()
                .Where(it => it.Kind == ArchiveKind.MicroFailure)
                .Where(it => it.OccurrenceCount >= minCount)
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. items
                .Where(it => !string.IsNullOrWhiteSpace(it.Content))
                .Select(it => it.Content!)];
        }

        private async Task<bool> MarkFedBackAsync(Guid id, int year, int weekOfYear)
        {
            var item = await _archiveRepository.GetByIdAsync(id).ConfigureAwait(false);
            if (item is null)
            {
                return false;
            }

            item.FedBackYear = year;
            item.FedBackToWeekOfYear = weekOfYear;
            item.UpdateTime = DateTime.Now;

            return await _archiveRepository.UpdateAsync(item,
                it => new { it.FedBackYear, it.FedBackToWeekOfYear, it.UpdateTime }).ConfigureAwait(false);
        }

        public async Task<bool> FeedBackAsync(Guid id, DateTime date)
        {
            var item = await _archiveRepository.GetByIdAsync(id).ConfigureAwait(false);
            //只有 7.4 有回流这回事；别的 List 的条目误传进来就直接拒绝，不要凭空造失败行
            if (item is null
                || item.Kind != ArchiveKind.MicroFailure
                || string.IsNullOrWhiteSpace(item.Content))
            {
                return false;
            }

            var (_, _, year, weekOfYear) = _scheduleService.GetWeek(date);

            //先建失败行再标记：反过来万一插行失败，这条就永远不再提醒了——宁可重复提醒，不可静默丢失
            bool inserted = await _blockRepository.InsertAsync(new StumblingBlockModel
            {
                Id = Guid.NewGuid(),
                Year = year,
                WeekOfYear = weekOfYear,
                Description = item.Content.Trim(),
                //回流条目不来自任何一天的日课，所以 SourceDiaryId 留空、
                //OrderIndex 记 0；SyncFromDailyAsync 只按 SourceDiaryId 查，扫不到它
                SourceDiaryId = null,
                OrderIndex = 0,
                Verdict = BlockVerdict.Undecided,
            }).ConfigureAwait(false);

            if (!inserted)
            {
                return false;
            }

            return await MarkFedBackAsync(id, year, weekOfYear).ConfigureAwait(false);
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return _archiveRepository.DeleteByIdAsync(id);
        }
    }
}
