using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 第 0 节的三个宏观方向（需求 11）。它们是全系统的锚：
    /// 周课 2.3 的 SMART 里那个 R（和更大方向一致吗？）就是对着它们答的。
    /// </summary>
    public interface IMacroGoalService
    {
        /// <summary>启用中的方向。周课挂靠时只用得着这些。</summary>
        Task<List<MacroGoalModel>> GetActiveAsync();

        Task<List<MacroGoalModel>> QueryAsync();

        Task<bool> SaveAsync(MacroGoalModel goal);

        Task<bool> DeleteAsync(Guid id);
    }
}
