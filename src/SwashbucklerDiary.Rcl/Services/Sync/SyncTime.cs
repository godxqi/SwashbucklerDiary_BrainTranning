namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 时间转换集中在这里，避免各处对 <see cref="DateTimeKind"/> 的理解不一致。
    /// <para>
    /// 数据库里的 <c>UpdateTime</c> 由 <c>DateTime.Now</c> 写入，Kind 是 Local。
    /// manifest 里统一存 Unix 毫秒，只用于比较，不用于展示。
    /// </para>
    /// </summary>
    public static class SyncTime
    {
        public static long ToUnixMs(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return new DateTimeOffset(value).ToUnixTimeMilliseconds();
            }

            // Unspecified 按本地时间解释——项目里所有时间都用 DateTime.Now 写入。
            return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local))
                .ToUnixTimeMilliseconds();
        }

        public static DateTime FromUnixMs(long value)
            => DateTimeOffset.FromUnixTimeMilliseconds(value).LocalDateTime;

        public static long ToUnixMsOrMin(DateTime value)
            => value == default ? long.MinValue : ToUnixMs(value);
    }
}
