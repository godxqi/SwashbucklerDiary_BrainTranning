using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class VirtueService : IVirtueService
    {
        private readonly IReviewRepository<VirtueEntryModel> _virtueRepository;

        public VirtueService(IReviewRepository<VirtueEntryModel> virtueRepository)
        {
            _virtueRepository = virtueRepository;
        }

        public Task<List<VirtueEntryModel>> QueryAsync(VirtueKind kind)
        {
            return _virtueRepository.Queryable()
                .Where(it => it.Kind == kind)
                .OrderByDescending(it => it.Date)
                .OrderByDescending(it => it.CreateTime)
                .ToListAsync();
        }

        public Task<bool> AddAsync(VirtueKind kind, string? content, DateTime? date = null, Guid? sourceDiaryId = null)
        {
            string value = content?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                //空内容当成功返回：调用方（2.4 结算）只有达成时才写成就，
                //目标内容为空是「没什么可记的」，不是失败，不该弹错误。
                return Task.FromResult(true);
            }

            //「达成」这件事语义上由 Kind == Achievement 表达，但 IsAchieved 这一列在老库里
            //是 NOT NULL 且删不掉（见模型上的注释），所以照写不误，两边都记上。
            bool achieved = kind == VirtueKind.Achievement;
            var now = DateTime.Now;

            return _virtueRepository.InsertAsync(new VirtueEntryModel
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Content = value,
                //补足过去的优点表时由页面给日期；不给就是「现在记的」
                Date = (date ?? now).Date,
                SourceDiaryId = sourceDiaryId,
                IsAchieved = achieved,
                AchievedTime = achieved ? now : null,
            });
        }

        public Task<bool> UpdateAsync(VirtueEntryModel entry)
        {
            entry.UpdateTime = DateTime.Now;
            return _virtueRepository.UpdateAsync(entry);
        }

        public Task<bool> DeleteAsync(Guid id)
        {
            return _virtueRepository.DeleteByIdAsync(id);
        }

        public async Task<List<AchievementGrowth>> GetAchievementGrowthAsync()
        {
            //只取成就那一栏、而且只取日期列：成就是「曾经渴望的一切」，天然只有几十条，
            //所以这里按月在 C# 里归并，不去冒 GroupBy(年,月) 那种 SQL 翻译的风险
            //（阶段 2 的教训：新写法的 SQL 一律要真机实测过才敢用）。
            //阶段 4 的统计页若要做更重的聚合，再换成 SQL 并补实测。
            var dates = await _virtueRepository.Queryable()
                .Where(it => it.Kind == VirtueKind.Achievement)
                .Select(it => it.Date)
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. dates
                .GroupBy(it => (it.Year, it.Month))
                .OrderBy(it => it.Key.Year)
                .ThenBy(it => it.Key.Month)
                .Select(it => new AchievementGrowth(it.Key.Year, it.Key.Month, it.Count()))];
        }
    }
}
