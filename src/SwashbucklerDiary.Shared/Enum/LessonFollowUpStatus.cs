namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 课题回访结果。到期后回来看一眼：下一步有没有做到、关系有没有变化。
    /// </summary>
    public enum LessonFollowUpStatus
    {
        /// <summary>做到了。</summary>
        Done = 0,

        /// <summary>部分做到。</summary>
        Partial = 1,

        /// <summary>没做到。</summary>
        NotDone = 2,

        /// <summary>情况已经变了，这条不再相关。</summary>
        Irrelevant = 3,
    }
}
