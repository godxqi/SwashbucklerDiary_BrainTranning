namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 一条归档项的入口（方法文档 7 各 List 的「入口」标注）。
    /// 与 <see cref="ArchiveKind"/>（落到哪个 List）正交：多个入口可以流进同一个 List。
    /// </summary>
    public enum ArchiveSource
    {
        /// <summary>手动补充。</summary>
        Manual = 0,

        /// <summary>周课 2.4：未达成 + 原因是环境 / 别人不配合。</summary>
        EnvironmentFailure = 1,

        /// <summary>周课 2.1：落选的绊脚石（每周只能升一条，其余进 7.4 存档）。</summary>
        RejectedBlock = 2,

        /// <summary>
        /// 月课 3.1 三列审视的产出。
        /// <para>
        /// 文档 7.1 的第二个入口「状态管理表的触因模式」也落在这里：触因要先经过月课 3.1
        /// 变成一条限制性观念才归档，所以它不需要单独一个来源。
        /// </para>
        /// </summary>
        SelfInsight = 4,

        /// <summary>周课 2.3 定下、跨周还没做的目标。</summary>
        WeeklyGoal = 5,

        // 数值不再连着：3 与 6 原本是 TriggerPattern / DailyThought，但两条路最后都并到了
        // SelfInsight 与 Manual（见上面的说明与 7.3 的入口），从没有东西写过它们，就删了。
        // 剩下的显式写死数值，是为了让**已经落库的行**继续读对——绝不重排既有取值。
    }
}
