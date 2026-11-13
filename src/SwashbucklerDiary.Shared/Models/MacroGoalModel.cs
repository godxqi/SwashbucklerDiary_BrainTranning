namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 第 0 节的三个宏观方向。它们是全系统的锚：
    /// 周目标 SMART 的 R（相关的：这个目标值得做吗？和更大方向一致吗？）就是对着它们答的。
    /// </summary>
    public class MacroGoalModel : BaseModel
    {
        /// <summary>试着把三个方向各挂到一个领域上；挂不上也没关系，说明那个方向暂时不需要每日喂养。</summary>
        public GoalDomain? Domain { get; set; }

        public string? Title { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
