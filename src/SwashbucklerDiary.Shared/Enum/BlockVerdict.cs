namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 周课 2.1 对一条微观失败的人工判定。机器只负责数频率，判断由人来做。
    /// </summary>
    public enum BlockVerdict
    {
        Undecided = 0,

        /// <summary>重复出现，是绊脚石 → 进 2.2 候选池。</summary>
        StumblingBlock = 1,

        /// <summary>原因是环境 / 别人不配合 → 归档到 7.4。</summary>
        Environment = 2,

        /// <summary>本周落选（每周最多升一条）→ 归档到 7.4 存档。</summary>
        Rejected = 3,
    }
}
