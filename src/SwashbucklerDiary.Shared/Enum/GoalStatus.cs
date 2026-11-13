namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 目标卡的生命周期。周课 2.4 结算时流转到 <see cref="Done"/> 或 <see cref="Dropped"/>。
    /// </summary>
    public enum GoalStatus
    {
        Pending = 0,
        InProgress = 1,
        Done = 2,
        Dropped = 3,
    }
}
