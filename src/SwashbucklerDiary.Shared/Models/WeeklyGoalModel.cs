namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 周课 2.3 的结构化目标卡。每周三个（情感 / 工作 / 个人发展各一），
    /// 结算是 2.4：<see cref="GoalStatus.Done"/> 进成就列表，未达成写原因。
    /// </summary>
    public class WeeklyGoalModel : BaseModel
    {
        public Guid WeeklyReviewId { get; set; }

        public GoalDomain Domain { get; set; }

        public string? Content { get; set; }

        /// <summary>对应的宏观方向（第 0 节）。SMART 的 R 就是对着它答的。</summary>
        public Guid? MacroGoalId { get; set; }

        public string? SmartSpecific { get; set; }

        public string? SmartMeasurable { get; set; }

        public string? SmartAchievable { get; set; }

        public string? SmartRelevant { get; set; }

        public string? SmartTimeBound { get; set; }

        public GoalStatus Status { get; set; }

        /// <summary>
        /// 2.4 结算时写下的说明。<b>2026-10-06 起不再写入</b>：未达成改成按 SMART 分项归因（见下面五个 Note），
        /// 再要一个总说明是重复。列留着，旧数据的值还在。
        /// </summary>
        public string? SettlementNote { get; set; }

        // ——— 2.1 未达成的归因（2026-10-06）———
        // 结算「未完成」时，逐项勾出「哪一项是没达成的原因」，勾了的要写清为什么。
        // **非空就等于勾了**，所以不另存一个勾选标记——五列都在，与上面五个 SMART 文本一一对称。
        // 新增列必须可空：SqlSugar 的 InitTables 给已有表补列时补的就是可空列，声明成非空会读一次炸一次。

        /// <summary>S 具体的 —— 没达成是因为目标本身没说清？写下原因。</summary>
        public string? SmartSpecificNote { get; set; }

        /// <summary>M 可衡量的 —— 没达成是因为没法判断做到没有？</summary>
        public string? SmartMeasurableNote { get; set; }

        /// <summary>A 可实现的 —— 没达成是因为现实条件下做不到？</summary>
        public string? SmartAchievableNote { get; set; }

        /// <summary>R 相关的 —— 没达成是因为这个目标不值得做 / 和更大方向不一致？</summary>
        public string? SmartRelevantNote { get; set; }

        /// <summary>T 有时限的 —— 没达成是因为没定什么时候完成？</summary>
        public string? SmartTimeBoundNote { get; set; }

        /// <summary>结算发生在哪一周（ISO 周次），配 <see cref="SettledYear"/> 使用。</summary>
        public int? SettledWeekOfYear { get; set; }

        public int? SettledYear { get; set; }
    }
}
