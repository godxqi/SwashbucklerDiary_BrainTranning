using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class MacroGoalService : IMacroGoalService
    {
        private readonly IReviewRepository<MacroGoalModel> _macroGoalRepository;

        public MacroGoalService(IReviewRepository<MacroGoalModel> macroGoalRepository)
        {
            _macroGoalRepository = macroGoalRepository;
        }

        public Task<List<MacroGoalModel>> GetActiveAsync()
        {
            return _macroGoalRepository.Queryable()
                .Where(it => it.IsActive)
                .OrderBy(it => it.CreateTime)
                .ToListAsync();
        }

        public Task<List<MacroGoalModel>> QueryAsync()
        {
            return _macroGoalRepository.Queryable()
                .OrderBy(it => it.CreateTime)
                .ToListAsync();
        }

        public Task<bool> SaveAsync(MacroGoalModel goal)
        {
            goal.UpdateTime = DateTime.Now;

            if (goal.Id == Guid.Empty)
            {
                goal.Id = Guid.NewGuid();
                return _macroGoalRepository.InsertAsync(goal);
            }

            return _macroGoalRepository.UpdateAsync(goal);
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return _macroGoalRepository.DeleteByIdAsync(id);
        }
    }
}
