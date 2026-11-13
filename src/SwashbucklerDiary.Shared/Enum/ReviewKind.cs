namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 一条日记在复盘流水线中的身份。null 等同于 <see cref="Free"/>。
    /// </summary>
    public enum ReviewKind
    {
        Free = 0,
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
    }
}
