using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class MonthlyReviewService : IMonthlyReviewService
    {
        private readonly IReviewRepository<MonthlyReviewModel> _monthlyReviewRepository;

        private readonly IReviewRepository<LimitingBeliefModel> _beliefRepository;

        private readonly IReviewRepository<EnemyModel> _enemyRepository;

        private readonly IAffirmationService _affirmationService;

        private readonly IReviewScheduleService _scheduleService;

        private readonly IDiaryService _diaryService;

        private readonly IMediaResourceManager _mediaResourceManager;

        private readonly II18nService _i18n;

        public MonthlyReviewService(IReviewRepository<MonthlyReviewModel> monthlyReviewRepository,
            IReviewRepository<LimitingBeliefModel> beliefRepository,
            IReviewRepository<EnemyModel> enemyRepository,
            IAffirmationService affirmationService,
            IReviewScheduleService scheduleService,
            IDiaryService diaryService,
            IMediaResourceManager mediaResourceManager,
            II18nService i18n)
        {
            _monthlyReviewRepository = monthlyReviewRepository;
            _beliefRepository = beliefRepository;
            _enemyRepository = enemyRepository;
            _affirmationService = affirmationService;
            _scheduleService = scheduleService;
            _diaryService = diaryService;
            _mediaResourceManager = mediaResourceManager;
            _i18n = i18n;
        }

        public async Task<MonthlyReviewModel?> GetAsync(int year, int month)
        {
            return await _monthlyReviewRepository.Queryable()
                .Where(it => it.Year == year && it.Month == month)
                .FirstAsync()
                .ConfigureAwait(false);
        }

        public async Task<MonthlyReviewModel> GetOrCreateAsync(DateTime date)
        {
            var (_, _, year, month) = _scheduleService.GetMonth(date);

            var existing = await GetAsync(year, month).ConfigureAwait(false);
            if (existing is not null)
            {
                return existing;
            }

            var created = new MonthlyReviewModel
            {
                Id = Guid.NewGuid(),
                Year = year,
                Month = month,
            };

            await _monthlyReviewRepository.InsertAsync(created).ConfigureAwait(false);
            return created;
        }

        // ———————————————— 3.1 三列审视 ————————————————

        public Task<List<LimitingBeliefModel>> GetBeliefsAsync(Guid monthlyReviewId)
        {
            if (monthlyReviewId == Guid.Empty)
            {
                return Task.FromResult<List<LimitingBeliefModel>>([]);
            }

            return _beliefRepository.Queryable()
                .Where(it => it.MonthlyReviewId == monthlyReviewId)
                .OrderBy(it => it.CreateTime)
                .ToListAsync();
        }

        public async Task<bool> SaveBeliefAsync(LimitingBeliefModel belief)
        {
            belief.UpdateTime = DateTime.Now;

            if (belief.Id == Guid.Empty)
            {
                belief.Id = Guid.NewGuid();
                return await _beliefRepository.InsertAsync(belief).ConfigureAwait(false);
            }

            return await _beliefRepository.UpdateAsync(belief).ConfigureAwait(false);
        }

        public Task<bool> DeleteBeliefAsync(Guid id)
        {
            return _beliefRepository.DeleteByIdAsync(id);
        }

        // ———————————————— 3.4 敌人表 ————————————————

        public Task<List<EnemyModel>> GetEnemiesAsync(Guid monthlyReviewId)
        {
            if (monthlyReviewId == Guid.Empty)
            {
                return Task.FromResult<List<EnemyModel>>([]);
            }

            return _enemyRepository.Queryable()
                .Where(it => it.MonthlyReviewId == monthlyReviewId)
                .OrderBy(it => it.CreateTime)
                .ToListAsync();
        }

        public async Task<bool> SaveEnemyAsync(EnemyModel enemy)
        {
            enemy.UpdateTime = DateTime.Now;

            if (enemy.Id == Guid.Empty)
            {
                enemy.Id = Guid.NewGuid();
                return await _enemyRepository.InsertAsync(enemy).ConfigureAwait(false);
            }

            return await _enemyRepository.UpdateAsync(enemy).ConfigureAwait(false);
        }

        public Task<bool> DeleteEnemyAsync(Guid id)
        {
            return _enemyRepository.DeleteByIdAsync(id);
        }

        // ———————————————— 3.2 出口 ————————————————

        public Task<bool> AddAffirmationAsync(MonthlyReviewModel review, string? phrase, string? source)
        {
            string value = phrase?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return Task.FromResult(false);
            }

            return _affirmationService.AddAsync(new SelfAffirmationModel
            {
                Id = Guid.NewGuid(),
                Content = value,
                Source = string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
                //这一列就是 3.2 闸门的依据，也是文档 4.2 那张表的「来自哪」
                SourceMonthlyReviewId = review.Id,
                IsActive = true,
            });
        }

        // ———————————————— 闸门与保存 ————————————————

        public async Task<MonthlyStepGates> GetGatesAsync(MonthlyReviewModel? review)
        {
            if (review is null)
            {
                return MonthlyStepGates.None;
            }

            var beliefs = await GetBeliefsAsync(review.Id).ConfigureAwait(false);
            var enemies = await GetEnemiesAsync(review.Id).ConfigureAwait(false);
            var affirmations = await _affirmationService
                .GetByMonthlyReviewAsync(review.Id)
                .ConfigureAwait(false);

            return new MonthlyStepGates(
                HasAssertion(beliefs),
                affirmations.Count > 0,
                !string.IsNullOrWhiteSpace(review.Step33Note),
                enemies.Count > 0);
        }

        public async Task<bool> SaveAsync(MonthlyReviewModel review, string? step33Note)
        {
            review.Step33Note = string.IsNullOrWhiteSpace(step33Note) ? null : step33Note.Trim();

            var beliefs = await GetBeliefsAsync(review.Id).ConfigureAwait(false);
            var enemies = await GetEnemiesAsync(review.Id).ConfigureAwait(false);
            var affirmations = await _affirmationService
                .GetByMonthlyReviewAsync(review.Id)
                .ConfigureAwait(false);

            review.Step31Done = HasAssertion(beliefs);
            review.Step32Done = affirmations.Count > 0;
            review.Step33Done = review.Step33Note is not null;
            review.Step34Done = enemies.Count > 0;

            string digest = MonthlyReviewSections.Compose(
                ReviewSectionText.MonthlyLabels(_i18n),
                beliefs,
                affirmations,
                review.Step33Note,
                enemies);

            var existingDiary = await FindDiaryAsync(review).ConfigureAwait(false);

            //一步都没做、而且这一月还没建过日记：不建空月课，只把闸门状态写回去。
            //已经建过就一定要往下走——用户可能是把月课的内容全清空了，正文里的摘要得跟着清。
            if (existingDiary is null && MonthlyReviewSections.IsEmpty(digest))
            {
                review.UpdateTime = DateTime.Now;
                return await _monthlyReviewRepository.UpdateAsync(review).ConfigureAwait(false);
            }

            var diary = existingDiary ?? await CreateDiaryAsync(review).ConfigureAwait(false);
            if (diary is null)
            {
                //日记没建起来就不该继续写 DiaryId，否则留下孤儿引用
                return false;
            }

            diary.Kind = ReviewKind.Monthly;
            diary.Template = false;
            diary.Title = $"{_i18n.T("Monthly review")} {review.Year:0000}/{review.Month:00}";
            //摘要是「生成」的，所以整块交给受管区块；区块之外用户后来在「库表」里
            //补写的内容每次保存都原样保留（Apply 内部先 Strip）
            diary.Content = ReviewContentBlock.Apply(diary.Content, digest);
            diary.Resources = _mediaResourceManager.GetDiaryResources(diary.Content);
            diary.UpdateTime = DateTime.Now;

            if (!await _diaryService.UpdateIncludesAsync(diary).ConfigureAwait(false))
            {
                return false;
            }

            review.DiaryId = diary.Id;
            //写了东西就不再算「跳过」——跳过的意思是「这段我真的没写」，写了就不成立了
            review.IsSkipped = false;
            review.UpdateTime = DateTime.Now;

            return await _monthlyReviewRepository.UpdateAsync(review).ConfigureAwait(false);
        }

        public async Task<bool> SetSkippedAsync(MonthlyReviewModel review, bool skipped)
        {
            review.IsSkipped = skipped;
            review.UpdateTime = DateTime.Now;

            return await _monthlyReviewRepository
                .UpdateAsync(review, it => new { it.IsSkipped, it.UpdateTime })
                .ConfigureAwait(false);
        }

        public async Task<bool> FinalizeAsync(MonthlyReviewModel review, string? step33Note)
        {
            //先把产出与摘要落盘，再立「已完成」的旗
            if (!await SaveAsync(review, step33Note).ConfigureAwait(false))
            {
                return false;
            }

            review.IsFinalized = true;
            review.FinalizedTime = DateTime.Now;
            review.UpdateTime = DateTime.Now;

            return await _monthlyReviewRepository
                .UpdateAsync(review, it => new
                {
                    it.Step31Done,
                    it.Step32Done,
                    it.Step33Done,
                    it.Step34Done,
                    it.Step33Note,
                    it.DiaryId,
                    it.IsFinalized,
                    it.FinalizedTime,
                    it.UpdateTime,
                })
                .ConfigureAwait(false);
        }

        /// <summary>3.1 的闸门：至少有一条真的写了观念。空行是点开对话框又退出来的残留。</summary>
        private static bool HasAssertion(List<LimitingBeliefModel> beliefs)
        {
            return beliefs.Any(it => !string.IsNullOrWhiteSpace(it.Assertion));
        }

        private async Task<DiaryModel?> CreateDiaryAsync(MonthlyReviewModel review)
        {
            var (_, end, _, _) = GetMonthRange(review);

            var created = new DiaryModel
            {
                Id = Guid.NewGuid(),
                Kind = ReviewKind.Monthly,
                //月课属于它结算的那个月，所以落在月末那天，而不是「今天」
                CreateTime = end.Date.Add(DateTime.Now.TimeOfDay),
                Tags = [],
                Resources = [],
            };

            return await _diaryService.AddAsync(created).ConfigureAwait(false) ? created : null;
        }

        private async Task<DiaryModel?> FindDiaryAsync(MonthlyReviewModel review)
        {
            if (review.DiaryId is Guid diaryId && diaryId != Guid.Empty)
            {
                var byId = await _diaryService.FindAsync(diaryId).ConfigureAwait(false);
                if (byId is not null)
                {
                    return byId;
                }
            }

            var (start, end, _, _) = GetMonthRange(review);
            var from = start.Date;
            var to = end.Date.AddDays(1);

            return await _diaryService
                .FindAsync(it => it.Kind == ReviewKind.Monthly && it.CreateTime >= from && it.CreateTime < to)
                .ConfigureAwait(false);
        }

        private (DateTime Start, DateTime End, int Year, int Month) GetMonthRange(MonthlyReviewModel review)
        {
            return _scheduleService.GetMonth(new DateTime(review.Year, review.Month, 1));
        }
    }
}
