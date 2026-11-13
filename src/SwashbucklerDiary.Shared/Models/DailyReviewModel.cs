namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 日课（方法文档 1）的结构化面。散文部分仍在 <see cref="DiaryModel.Content"/> 里，
    /// 这里只放需要计数 / 聚合的字段。
    /// </summary>
    public class DailyReviewModel : BaseModel
    {
        /// <summary>对应那篇日记。日历 / 搜索 / 导出 / 隐私模式都由它继承。</summary>
        public Guid DiaryId { get; set; }

        /// <summary>这一天（只有日期部分有意义，写入前需归一化到零点）。每天一条。</summary>
        public DateTime Date { get; set; }

        /// <summary>1.6 情绪命名。八大情绪之一。</summary>
        public MoodKind? Mood { get; set; }

        /// <summary>1.7 重复了哪几条自我肯定（JSON 序列化的 List&lt;Guid&gt;）。它是选取记录，计数在 SelfAffirmationModel.RepeatCount。</summary>
        public string? AffirmationIds { get; set; }

        /// <summary>日课是否已完成。</summary>
        public bool IsCompleted { get; set; }
    }
}
