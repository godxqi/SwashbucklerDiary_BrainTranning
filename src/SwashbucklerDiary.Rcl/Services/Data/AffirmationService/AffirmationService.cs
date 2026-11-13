using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class AffirmationService : IAffirmationService
    {
        private readonly IReviewRepository<SelfAffirmationModel> _affirmationRepository;

        public AffirmationService(IReviewRepository<SelfAffirmationModel> affirmationRepository)
        {
            _affirmationRepository = affirmationRepository;
        }

        public async Task<List<SelfAffirmationModel>> GetActiveAsync(int count = 0)
        {
            //SqlSugar 的 OrderBy 是追加而不是覆盖，所以这两句合成 ORDER BY RepeatCount ASC, CreateTime DESC
            var query = _affirmationRepository.Queryable()
                .Where(it => it.IsActive)
                .OrderBy(it => it.RepeatCount)
                .OrderByDescending(it => it.CreateTime);

            if (count > 0)
            {
                query = query.Take(count);
            }

            return await query.ToListAsync().ConfigureAwait(false);
        }

        public Task<List<SelfAffirmationModel>> QueryAsync()
        {
            return _affirmationRepository.Queryable()
                .OrderByDescending(it => it.CreateTime)
                .ToListAsync();
        }

        public Task<List<SelfAffirmationModel>> GetByMonthlyReviewAsync(Guid monthlyReviewId)
        {
            if (monthlyReviewId == Guid.Empty)
            {
                return Task.FromResult<List<SelfAffirmationModel>>([]);
            }

            return _affirmationRepository.Queryable()
                .Where(it => it.SourceMonthlyReviewId == monthlyReviewId)
                .OrderBy(it => it.CreateTime)
                .ToListAsync();
        }

        public Task<bool> AddAsync(SelfAffirmationModel affirmation)
        {
            if (affirmation.Id == Guid.Empty)
            {
                affirmation.Id = Guid.NewGuid();
            }

            return _affirmationRepository.InsertAsync(affirmation);
        }

        public Task<bool> UpdateAsync(SelfAffirmationModel affirmation)
        {
            affirmation.UpdateTime = DateTime.Now;
            return _affirmationRepository.UpdateAsync(affirmation);
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return _affirmationRepository.DeleteByIdAsync(id);
        }

        public async Task<SelfAffirmationModel?> RepeatAsync(Guid id)
        {
            var affirmation = await _affirmationRepository.GetByIdAsync(id).ConfigureAwait(false);
            if (affirmation is null)
            {
                return null;
            }

            affirmation.RepeatCount++;
            affirmation.UpdateTime = DateTime.Now;

            bool success = await _affirmationRepository.UpdateAsync(affirmation).ConfigureAwait(false);
            return success ? affirmation : null;
        }
    }
}
