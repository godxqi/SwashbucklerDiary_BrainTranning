namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 四个 List · 归档区（方法文档 7）的一条。
    /// 归档区不是待办——只有周课 2.5 的出口问题才往这里写。
    /// </summary>
    public class ArchiveItemModel : BaseModel
    {
        /// <summary>落在哪个 List（7.1 ~ 7.4）。</summary>
        public ArchiveKind Kind { get; set; }

        /// <summary>从哪个入口进来的。</summary>
        public ArchiveSource Source { get; set; }

        public string? Content { get; set; }

        /// <summary>在归档区出现了几次。7.4 累计 3 次就要回流。</summary>
        public int OccurrenceCount { get; set; }

        public DateTime LastOccurredTime { get; set; }

        public Guid? SourceWeeklyReviewId { get; set; }

        /// <summary>已经回流到哪一周。非空表示已回流，不再重复提醒。配 <see cref="FedBackYear"/> 使用。</summary>
        public int? FedBackToWeekOfYear { get; set; }

        public int? FedBackYear { get; set; }
    }
}
