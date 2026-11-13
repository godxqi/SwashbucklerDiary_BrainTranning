using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 自我肯定库（方法文档 4）。书里说的是 3~4 条短语，每天多次用心重复。
    /// </summary>
    public interface IAffirmationService
    {
        /// <summary>按重复次数从少到多取启用的条目——越用得少越该被看到。</summary>
        Task<List<SelfAffirmationModel>> GetActiveAsync(int count = 0);

        Task<List<SelfAffirmationModel>> QueryAsync();

        /// <summary>
        /// 某一次月课产出的短语（3.2 与 3.4 打击手段①）。
        /// 月课 3.2 那一步的闸门就是「这次的产量 ≥ 1」，靠它判得出来。
        /// </summary>
        Task<List<SelfAffirmationModel>> GetByMonthlyReviewAsync(Guid monthlyReviewId);

        Task<bool> AddAsync(SelfAffirmationModel affirmation);

        Task<bool> UpdateAsync(SelfAffirmationModel affirmation);

        Task<bool> DeleteAsync(Guid id);

        /// <summary>复盘主页顶部条点一下 = 重复一次并计数。返回更新后的条目。</summary>
        Task<SelfAffirmationModel?> RepeatAsync(Guid id);
    }
}
