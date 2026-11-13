namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 四个 List · 归档区（方法文档 7）。
    /// 归档区不是待办，入口是周课 2.4 的出口问题（以及各个来源的自动归档）。
    /// </summary>
    public enum ArchiveKind
    {
        /// <summary>7.1 对自我的新认识 / 自我的状态。</summary>
        SelfInsight = 0,

        /// <summary>7.2 对未来的新计划。</summary>
        FuturePlan = 1,

        /// <summary>7.3 对日常的思考。</summary>
        DailyThought = 2,

        /// <summary>7.4 经历的微小失败。唯一带回流计数的一张。</summary>
        MicroFailure = 3,
    }
}
