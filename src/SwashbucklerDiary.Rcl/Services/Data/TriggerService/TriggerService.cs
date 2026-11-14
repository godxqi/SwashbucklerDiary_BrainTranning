using SqlSugar;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class TriggerService : ITriggerService
    {
        private readonly ITagService _tagService;

        private readonly IReviewRepository<StateRecordModel> _stateRepository;

        private readonly IReviewRepository<LessonModel> _lessonRepository;

        public TriggerService(ITagService tagService,
            IReviewRepository<StateRecordModel> stateRepository,
            IReviewRepository<LessonModel> lessonRepository)
        {
            _tagService = tagService;
            _stateRepository = stateRepository;
            _lessonRepository = lessonRepository;
        }

        public async Task<List<TriggerCount>> GetWithCountsAsync()
        {
            //只认显式标了 Trigger 的标签：老标签的 Kind 是 NULL，语义上等于
            //TagKind.Normal，这里不该被当成触因。可空枚举的比较由 SqlSugar 翻成 Kind = 1。
            var tags = await _tagService
                .QueryAsync(it => it.Kind == TagKind.Trigger)
                .ConfigureAwait(false);

            if (tags.Count == 0)
            {
                return [];
            }

            var counts = await GetOccurrenceCountsAsync().ConfigureAwait(false);

            return [.. tags
                .Select(tag => new TriggerCount(tag, counts.GetValueOrDefault(tag.Id)))
                .OrderByDescending(it => it.Count)
                .ThenBy(it => it.Tag.Name, StringComparer.CurrentCulture)];
        }

        public async Task<List<TriggerCount>> GetReadyForMonthlyAsync(int minCount = 3)
        {
            var all = await GetWithCountsAsync().ConfigureAwait(false);
            return [.. all.Where(it => it.Count >= minCount)];
        }

        public async Task<bool> AddAsync(string name)
        {
            string value = name?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return false;
            }

            //同名触因直接复用，否则「攒够 3 次」会被同义词拆散
            var existing = await _tagService
                .FindAsync(it => it.Name == value && it.Kind == TagKind.Trigger)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                return true;
            }

            return await _tagService.AddAsync(new TagModel
            {
                Id = Guid.NewGuid(),
                Name = value,
                Kind = TagKind.Trigger,
            }).ConfigureAwait(false);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            //先把指向它的状态记录处理掉，别留下孤儿引用：
            //「记一次」那种只填了触因的空壳直接删；写过长内容（场景 / 四种感受）的保留正文，只摘掉挂靠。
            var records = await _stateRepository.Queryable()
                .Where(it => it.TriggerTagId == id)
                .ToListAsync()
                .ConfigureAwait(false);

            var quickLogs = records.Where(it => it.IsTriggerOnly()).ToList();
            var written = records.Where(it => !it.IsTriggerOnly()).ToList();

            if (quickLogs.Count > 0)
            {
                await _stateRepository.DeleteAsync(quickLogs).ConfigureAwait(false);
            }

            if (written.Count > 0)
            {
                foreach (var record in written)
                {
                    record.TriggerTagId = null;
                    record.UpdateTime = DateTime.Now;
                }

                await _stateRepository
                    .UpdateAsync(written, it => new { it.TriggerTagId, it.UpdateTime })
                    .ConfigureAwait(false);
            }

            //课题里的触因也指向这个标签；不清掉就会留下孤儿引用
            var lessons = await _lessonRepository.Queryable()
                .Where(it => it.TriggerTagId == id)
                .ToListAsync()
                .ConfigureAwait(false);
            if (lessons.Count > 0)
            {
                foreach (var lesson in lessons)
                {
                    lesson.TriggerTagId = null;
                    lesson.UpdateTime = DateTime.Now;
                }

                await _lessonRepository
                    .UpdateAsync(lessons, it => new { it.TriggerTagId, it.UpdateTime })
                    .ConfigureAwait(false);
            }

            return await _tagService.DeleteAsync(id).ConfigureAwait(false);
        }

        public async Task<bool> RecordOccurrenceAsync(Guid tagId, DateTime? occurredTime = null)
        {
            if (tagId == Guid.Empty)
            {
                return false;
            }

            return await _stateRepository.InsertAsync(new StateRecordModel
            {
                Id = Guid.NewGuid(),
                OccurredTime = occurredTime ?? DateTime.Now,
                TriggerTagId = tagId,
            }).ConfigureAwait(false);
        }

        /// <summary>每个触因在状态管理表里出现过几次。走 GroupBy 聚合，不在 C# 里遍历全表。</summary>
        private async Task<Dictionary<Guid, int>> GetOccurrenceCountsAsync()
        {
            var rows = await _stateRepository.Queryable()
                .Where(it => it.TriggerTagId != null)
                .GroupBy(it => it.TriggerTagId)
                .Select(it => new
                {
                    TriggerTagId = it.TriggerTagId,
                    Count = SqlFunc.AggregateCount(it.Id),
                })
                .ToListAsync()
                .ConfigureAwait(false);

            Dictionary<Guid, int> counts = rows
                .Where(it => it.TriggerTagId is not null)
                .ToDictionary(it => it.TriggerTagId!.Value, it => it.Count);

            //课题里的触因也算「同类攒了几个」：周课 2.2 与月课 3.1 看的是同一份次数
            var lessonRows = await _lessonRepository.Queryable()
                .Where(it => it.TriggerTagId != null)
                .GroupBy(it => it.TriggerTagId)
                .Select(it => new
                {
                    TriggerTagId = it.TriggerTagId,
                    Count = SqlFunc.AggregateCount(it.Id),
                })
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var row in lessonRows)
            {
                if (row.TriggerTagId is Guid id)
                {
                    counts[id] = counts.GetValueOrDefault(id) + row.Count;
                }
            }

            return counts;
        }
    }
}
