namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 月课 3.4 三列三行敌人表的一行。
    /// 与 3.1 的区别：3.1 针对信念，这张表针对目标——扭转顽固的模式，而非设定新的意图。
    /// </summary>
    public class EnemyModel : BaseModel
    {
        public Guid MonthlyReviewId { get; set; }

        /// <summary>第一列：一个已经计划了很久却一直没实现的目标。</summary>
        public string? Goal { get; set; }

        /// <summary>第二列：诚实地记录阻止你实现目标的行为。写行为，不写想法。</summary>
        public string? BlockingBehavior { get; set; }

        /// <summary>第三列：驱动这种行为的深层观念。</summary>
        public string? DeepBelief { get; set; }

        /// <summary>打击手段③：每个目标配一项本周可完成的具体操作。最容易被跳过的一步。</summary>
        public string? WeeklyAction { get; set; }
    }
}
