namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 状态管理表（方法文档 5）的一条。随时可记，不是每日节拍的一部分。
    /// 出口是 5.2：同类触因攒够 3 个就交给月课 3.1 的第一列。
    /// </summary>
    public class StateRecordModel : BaseModel
    {
        public DateTime OccurredTime { get; set; }

        /// <summary>当前场景。</summary>
        public string? Scene { get; set; }

        /// <summary>触发诱因。指向 TagModel（Kind = Trigger），这样"同类攒 3 个"能自动算。</summary>
        public Guid? TriggerTagId { get; set; }

        /// <summary>当前感受 · 身体上的。</summary>
        public string? Body { get; set; }

        /// <summary>当前感受 · 心理上的（你在想什么）。</summary>
        public string? Mind { get; set; }

        /// <summary>当前感受 · 情感上的（你的感受）。</summary>
        public string? Emotion { get; set; }

        /// <summary>当前感受 · 精神上的（意义感、使命感、位置感）。</summary>
        public string? Spirit { get; set; }

        /// <summary>这条情绪叫什么名字。八大情绪之一。</summary>
        public MoodKind? EmotionName { get; set; }

        /// <summary>现在要调用哪一条。这一格是全表最重要的一格。</summary>
        public InvokedKind? InvokedKind { get; set; }

        public Guid? InvokedId { get; set; }

        /// <summary>现在需要做什么。最低门槛动作即可，兜底方案是上床睡觉。</summary>
        public string? ActionNow { get; set; }
    }
}
