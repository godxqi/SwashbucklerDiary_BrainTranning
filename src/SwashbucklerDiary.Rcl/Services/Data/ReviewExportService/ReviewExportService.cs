using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class ReviewExportService : IReviewExportService
    {
        private readonly IReviewRepository<DailyReviewModel> _dailyRepository;

        private readonly IReviewRepository<WeeklyReviewModel> _weeklyRepository;

        private readonly IReviewRepository<WeeklyGoalModel> _weeklyGoalRepository;

        private readonly IReviewRepository<StumblingBlockModel> _stumblingBlockRepository;

        private readonly IReviewRepository<MonthlyReviewModel> _monthlyRepository;

        private readonly IReviewRepository<LimitingBeliefModel> _limitingBeliefRepository;

        private readonly IReviewRepository<EnemyModel> _enemyRepository;

        private readonly IReviewRepository<VirtueEntryModel> _virtueEntryRepository;

        private readonly IReviewRepository<SelfAffirmationModel> _affirmationRepository;

        private readonly IReviewRepository<ArchiveItemModel> _archiveItemRepository;

        private readonly IReviewRepository<MacroGoalModel> _macroGoalRepository;

        private readonly IReviewRepository<StateRecordModel> _stateRecordRepository;

        public ReviewExportService(
            IReviewRepository<DailyReviewModel> dailyRepository,
            IReviewRepository<WeeklyReviewModel> weeklyRepository,
            IReviewRepository<WeeklyGoalModel> weeklyGoalRepository,
            IReviewRepository<StumblingBlockModel> stumblingBlockRepository,
            IReviewRepository<MonthlyReviewModel> monthlyRepository,
            IReviewRepository<LimitingBeliefModel> limitingBeliefRepository,
            IReviewRepository<EnemyModel> enemyRepository,
            IReviewRepository<VirtueEntryModel> virtueEntryRepository,
            IReviewRepository<SelfAffirmationModel> affirmationRepository,
            IReviewRepository<ArchiveItemModel> archiveItemRepository,
            IReviewRepository<MacroGoalModel> macroGoalRepository,
            IReviewRepository<StateRecordModel> stateRecordRepository)
        {
            _dailyRepository = dailyRepository;
            _weeklyRepository = weeklyRepository;
            _weeklyGoalRepository = weeklyGoalRepository;
            _stumblingBlockRepository = stumblingBlockRepository;
            _monthlyRepository = monthlyRepository;
            _limitingBeliefRepository = limitingBeliefRepository;
            _enemyRepository = enemyRepository;
            _virtueEntryRepository = virtueEntryRepository;
            _affirmationRepository = affirmationRepository;
            _archiveItemRepository = archiveItemRepository;
            _macroGoalRepository = macroGoalRepository;
            _stateRecordRepository = stateRecordRepository;
        }

        public async Task<ReviewExportModel> ExportAsync()
        {
            return new ReviewExportModel
            {
                DailyReviews = await LoadAsync(_dailyRepository).ConfigureAwait(false),
                WeeklyReviews = await LoadAsync(_weeklyRepository).ConfigureAwait(false),
                WeeklyGoals = await LoadAsync(_weeklyGoalRepository).ConfigureAwait(false),
                StumblingBlocks = await LoadAsync(_stumblingBlockRepository).ConfigureAwait(false),
                MonthlyReviews = await LoadAsync(_monthlyRepository).ConfigureAwait(false),
                LimitingBeliefs = await LoadAsync(_limitingBeliefRepository).ConfigureAwait(false),
                Enemies = await LoadAsync(_enemyRepository).ConfigureAwait(false),
                VirtueEntries = await LoadAsync(_virtueEntryRepository).ConfigureAwait(false),
                Affirmations = await LoadAsync(_affirmationRepository).ConfigureAwait(false),
                ArchiveItems = await LoadAsync(_archiveItemRepository).ConfigureAwait(false),
                MacroGoals = await LoadAsync(_macroGoalRepository).ConfigureAwait(false),
                StateRecords = await LoadAsync(_stateRecordRepository).ConfigureAwait(false),
            };
        }

        public async Task ImportAsync(ReviewExportModel? model)
        {
            if (model is null)
            {
                return;
            }

            //顺序无关：这些表之间没有数据库级别的外键约束，引用是应用层靠 Guid 对的，
            //而 Guid 原样来自导出文件，所以先插哪张都不会产生悬空引用。
            //另外这里没有包显式事务——日记本体的导入路径（DiaryRepository.ImportAsync）也没有，
            //保持同一取舍；真出现半途失败，整库备份那条路可以还原。
            await MergeAsync(_dailyRepository, model.DailyReviews).ConfigureAwait(false);
            await MergeAsync(_weeklyRepository, model.WeeklyReviews).ConfigureAwait(false);
            await MergeAsync(_weeklyGoalRepository, model.WeeklyGoals).ConfigureAwait(false);
            await MergeAsync(_stumblingBlockRepository, model.StumblingBlocks).ConfigureAwait(false);
            await MergeAsync(_monthlyRepository, model.MonthlyReviews).ConfigureAwait(false);
            await MergeAsync(_limitingBeliefRepository, model.LimitingBeliefs).ConfigureAwait(false);
            await MergeAsync(_enemyRepository, model.Enemies).ConfigureAwait(false);
            await MergeAsync(_virtueEntryRepository, model.VirtueEntries).ConfigureAwait(false);
            await MergeAsync(_affirmationRepository, model.Affirmations).ConfigureAwait(false);
            await MergeAsync(_archiveItemRepository, model.ArchiveItems).ConfigureAwait(false);
            await MergeAsync(_macroGoalRepository, model.MacroGoals).ConfigureAwait(false);
            await MergeAsync(_stateRecordRepository, model.StateRecords).ConfigureAwait(false);
        }

        private static async Task<List<T>> LoadAsync<T>(IReviewRepository<T> repository)
            where T : class, new()
        {
            return await repository.Queryable().ToListAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 按主键 upsert。先在 SQL 侧一次问出「这些 Id 里哪些已经在了」，
        /// 再分两批走批量插入 / 逐条覆盖——不逐条先查一遍。
        /// </summary>
        private static async Task MergeAsync<T>(IReviewRepository<T> repository, List<T>? incoming)
            where T : BaseModel, new()
        {
            var items = incoming?.Where(it => it.Id != Guid.Empty).ToList();
            if (items is null || items.Count == 0)
            {
                return;
            }

            var ids = items.Select(it => it.Id).Distinct().ToList();
            var existingIds = await repository.Queryable()
                .Where(it => ids.Contains(it.Id))
                .Select(it => it.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            var existing = existingIds.ToHashSet();
            var toInsert = items.Where(it => !existing.Contains(it.Id)).ToList();
            var toUpdate = items.Where(it => existing.Contains(it.Id)).ToList();

            if (toInsert.Count > 0)
            {
                await repository.InsertRangeAsync(toInsert).ConfigureAwait(false);
            }

            foreach (var entity in toUpdate)
            {
                entity.UpdateTime = DateTime.Now;
                await repository.UpdateAsync(entity).ConfigureAwait(false);
            }
        }
    }
}
