namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 状态管理表 5.1「现在要调用哪一条」指向的库表。
    /// 这一格是全表最重要的一格：三个优点表就是为这一刻建的，不在这里调用它们就只是收藏。
    /// </summary>
    public enum InvokedKind
    {
        /// <summary>三个优点表（6.1 / 6.2 / 6.3）中的一条。</summary>
        Virtue = 0,

        /// <summary>自我肯定库（4.2）中的一条。</summary>
        Affirmation = 1,

        /// <summary>宏观目标（第 0 节的三个方向）中的一条。</summary>
        MacroGoal = 2,
    }
}
