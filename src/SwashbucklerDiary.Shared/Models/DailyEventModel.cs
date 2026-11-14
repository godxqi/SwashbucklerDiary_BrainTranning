namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 日课 1.1「生活上的」的一行：一件事，可挂一个人物标签。
    /// <para>
    /// 它是日课的副表，和 <see cref="DailyReviewModel"/> 一样只放需要查询的字段；
    /// 正文里的 1.1 由这些行渲染成受管区块。
    /// </para>
    /// </summary>
    public class DailyEventModel : BaseModel
    {
        /// <summary>对应哪次日课。</summary>
        public Guid DailyReviewId { get; set; }

        /// <summary>这件事和谁有关。可为空：没挂人的行只留在日课里。</summary>
        public Guid? PersonTagId { get; set; }

        public string? Content { get; set; }

        /// <summary>在一日之内的顺序，从 0 开始。</summary>
        public int OrderIndex { get; set; }
    }
}
