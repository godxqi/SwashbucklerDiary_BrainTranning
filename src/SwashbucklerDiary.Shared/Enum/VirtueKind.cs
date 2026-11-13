namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 三个优点表（方法文档 6）。它们的唯一用法是在状态糟糕时被调用。
    /// </summary>
    public enum VirtueKind
    {
        /// <summary>6.1 积极列表：让你感觉良好的朋友和家人 + 自我欣赏列表。</summary>
        Positive = 0,

        /// <summary>6.2 感恩列表。</summary>
        Gratitude = 1,

        /// <summary>6.3 成就列表。周课目标达成会自动写入这里。</summary>
        Achievement = 2,
    }
}
