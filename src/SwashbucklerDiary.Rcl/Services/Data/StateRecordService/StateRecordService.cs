using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class StateRecordService : IStateRecordService
    {
        private readonly IReviewRepository<StateRecordModel> _stateRepository;

        private readonly IReviewRepository<VirtueEntryModel> _virtueRepository;

        private readonly IReviewRepository<SelfAffirmationModel> _affirmationRepository;

        private readonly IReviewRepository<MacroGoalModel> _macroGoalRepository;

        private readonly ITagService _tagService;

        public StateRecordService(IReviewRepository<StateRecordModel> stateRepository,
            IReviewRepository<VirtueEntryModel> virtueRepository,
            IReviewRepository<SelfAffirmationModel> affirmationRepository,
            IReviewRepository<MacroGoalModel> macroGoalRepository,
            ITagService tagService)
        {
            _stateRepository = stateRepository;
            _virtueRepository = virtueRepository;
            _affirmationRepository = affirmationRepository;
            _macroGoalRepository = macroGoalRepository;
            _tagService = tagService;
        }

        public async Task<List<StateRecordView>> QueryAsync()
        {
            var records = await _stateRepository.Queryable()
                .OrderByDescending(it => it.OccurredTime)
                .ToListAsync()
                .ConfigureAwait(false);

            return await ToViewsAsync(records).ConfigureAwait(false);
        }

        public async Task<StateRecordView?> GetAsync(Guid id)
        {
            var record = await _stateRepository.GetByIdAsync(id).ConfigureAwait(false);
            if (record is null)
            {
                return null;
            }

            var views = await ToViewsAsync([record]).ConfigureAwait(false);
            return views.Count == 0 ? null : views[0];
        }

        public async Task<Guid?> SaveAsync(StateRecordModel record)
        {
            if (record.IsEmpty())
            {
                return null;
            }

            record.UpdateTime = DateTime.Now;

            if (record.Id == Guid.Empty)
            {
                record.Id = Guid.NewGuid();
                record.CreateTime = DateTime.Now;
                if (record.OccurredTime == default)
                {
                    record.OccurredTime = DateTime.Now;
                }

                return await _stateRepository.InsertAsync(record).ConfigureAwait(false)
                    ? record.Id
                    : null;
            }

            //更新时不覆盖 CreateTime：那是「什么时候记的」，不是本次保存时间
            var existing = await _stateRepository.GetByIdAsync(record.Id).ConfigureAwait(false);
            if (existing is null)
            {
                return null;
            }

            record.CreateTime = existing.CreateTime;
            return await _stateRepository.UpdateAsync(record).ConfigureAwait(false)
                ? record.Id
                : null;
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return _stateRepository.DeleteByIdAsync(id);
        }

        /// <summary>
        /// 把跨表的两个引用一次解析好。触因名在 <c>TagModel</c> 里，「调用哪一条」分散在
        /// 三个优点表 / 肯定库 / 宏观目标里——按引用类型分组，每组一次查询，不在 C# 里逐条翻库。
        /// </summary>
        private async Task<List<StateRecordView>> ToViewsAsync(List<StateRecordModel> records)
        {
            if (records.Count == 0)
            {
                return [];
            }

            var triggerNames = await GetTriggerNamesAsync(records).ConfigureAwait(false);
            var invokedTexts = await GetInvokedTextsAsync(records).ConfigureAwait(false);

            return [.. records.Select(record => new StateRecordView(
                record,
                record.TriggerTagId is Guid triggerId ? triggerNames.GetValueOrDefault(triggerId) : null,
                record.InvokedKind is InvokedKind kind && record.InvokedId is Guid invokedId
                    ? invokedTexts.GetValueOrDefault((kind, invokedId))
                    : null))];
        }

        private async Task<Dictionary<Guid, string>> GetTriggerNamesAsync(List<StateRecordModel> records)
        {
            var ids = records
                .Where(it => it.TriggerTagId is not null)
                .Select(it => it.TriggerTagId!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return [];
            }

            var tags = await _tagService
                .QueryAsync(it => ids.Contains(it.Id))
                .ConfigureAwait(false);

            return tags
                .Where(it => !string.IsNullOrWhiteSpace(it.Name))
                .ToDictionary(it => it.Id, it => it.Name!);
        }

        private async Task<Dictionary<(InvokedKind Kind, Guid Id), string>> GetInvokedTextsAsync(
            List<StateRecordModel> records)
        {
            Dictionary<(InvokedKind, Guid), string> texts = [];

            var groups = records
                .Where(it => it.InvokedKind is not null && it.InvokedId is not null)
                .GroupBy(it => it.InvokedKind!.Value);

            foreach (var group in groups)
            {
                var ids = group.Select(it => it.InvokedId!.Value).Distinct().ToList();

                //三张表只是「仓储 + 取哪个字段当正文」不同，逐张写一遍 case 就是同一段逻辑抄三次
                switch (group.Key)
                {
                    case InvokedKind.Virtue:
                        await CollectAsync(_virtueRepository, InvokedKind.Virtue, ids, it => it.Content)
                            .ConfigureAwait(false);
                        break;

                    case InvokedKind.Affirmation:
                        await CollectAsync(_affirmationRepository, InvokedKind.Affirmation, ids, it => it.Content)
                            .ConfigureAwait(false);
                        break;

                    default:
                        await CollectAsync(_macroGoalRepository, InvokedKind.MacroGoal, ids, it => it.Title)
                            .ConfigureAwait(false);
                        break;
                }
            }

            return texts;

            async Task CollectAsync<T>(
                IReviewRepository<T> repository,
                InvokedKind kind,
                List<Guid> ids,
                Func<T, string?> pick)
                where T : BaseModel, new()
            {
                var rows = await repository.Queryable()
                    .Where(it => ids.Contains(it.Id))
                    .ToListAsync()
                    .ConfigureAwait(false);

                foreach (var row in rows)
                {
                    string? text = pick(row);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        texts[(kind, row.Id)] = text;
                    }
                }
            }
        }
    }
}
