namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 课题记录（方法文档 4）的一条。它不再是日记：内容全在列里，方便按人物、类型、回访筛选。
    /// <para>
    /// 人物和触因都指向 <see cref="TagModel"/>，与现有标签体系共用；
    /// 类型是多选，落在 <see cref="LessonTypeLinkModel"/>。
    /// </para>
    /// </summary>
    public class LessonModel : BaseModel
    {
        /// <summary>这件事发生在什么时候。回访到期按它加 14 天。</summary>
        public DateTime OccurredTime { get; set; }

        /// <summary>和谁。指向 <see cref="TagModel"/>（Kind = Person）。</summary>
        public Guid? PersonTagId { get; set; }

        /// <summary>触因。指向 <see cref="TagModel"/>（Kind = Trigger），与状态管理表共用。</summary>
        public Guid? TriggerTagId { get; set; }

        /// <summary>4.2 事件：只写可观察的行为和原话。</summary>
        public string? Event { get; set; }

        /// <summary>4.3 我的反应 · 身体。</summary>
        public string? ReactionBody { get; set; }

        /// <summary>4.3 我的反应 · 想法。</summary>
        public string? ReactionMind { get; set; }

        /// <summary>4.3 我的反应 · 情绪。</summary>
        public string? ReactionEmotion { get; set; }

        /// <summary>4.3 我的反应 · 意义感。</summary>
        public string? ReactionSpirit { get; set; }

        /// <summary>4.4 照见。</summary>
        public string? Mirror { get; set; }

        /// <summary>4.5 边界。</summary>
        public string? Boundary { get; set; }

        /// <summary>4.6 带走一句。</summary>
        public string? Takeaway { get; set; }

        /// <summary>4.7 下一步。</summary>
        public string? NextStep { get; set; }

        /// <summary>
        /// 回访到期时间。默认是发生日 + 14 天。
        /// 想取消回访时写 <see cref="LessonFollowUpStatus.Irrelevant"/>，不要只清空日期——
        /// 服务层会把空日期重新补成默认值。
        /// </summary>
        public DateTime? FollowUpDueTime { get; set; }

        /// <summary>回访结果。空表示还没回访。</summary>
        public LessonFollowUpStatus? FollowUpStatus { get; set; }

        /// <summary>回访备注：发生了什么变化。</summary>
        public string? FollowUpNote { get; set; }
    }
}
