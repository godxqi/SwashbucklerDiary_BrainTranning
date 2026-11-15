namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 阶段 1 的临时诊断：记录 AOP 最近一次看到的操作类型。
    /// SqlSugar 的 AOT 精简包裁掉了 <c>DataOperationType</c> 枚举，运行时 <c>ToString()</c>
    /// 实测返回 <c>UpdateByObject</c> 这类带后缀的值；自检靠这里把实际值暴露出来。
    /// 阶段 6 删除。
    /// </summary>
    public static class SyncDiagnostics
    {
        public static string? LastOperationType { get; set; }

        public static string? LastPropertyName { get; set; }
    }

    /// <summary>
    /// 同步期间对全局行为的临时抑制开关。
    /// <para>
    /// 两个用途：
    /// <list type="number">
    /// <item>从远端应用对象时必须保留对象自带的 <c>UpdateTime</c>，否则会被 AOP 改写成"现在"，LWW 失效；</item>
    /// <item>同步应用删除时不能再写墓碑，否则删除会在两台设备之间来回震荡。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 用 <see cref="AsyncLocal{T}"/> 而不是静态 bool：同步可能在后台任务里跑，
    /// 静态标志会把同一进程里用户的手动编辑也一起抑制掉。
    /// </para>
    /// </summary>
    public static class SyncContext
    {
        private static readonly AsyncLocal<bool> _suppressUpdateTimeRefresh = new();

        private static readonly AsyncLocal<bool> _suppressGraveRecording = new();

        public static bool SuppressUpdateTimeRefresh => _suppressUpdateTimeRefresh.Value;

        public static bool SuppressGraveRecording => _suppressGraveRecording.Value;

        /// <summary>进入"保留原 UpdateTime"的作用域，<c>using</c> 结束后自动恢复。</summary>
        public static IDisposable BeginSuppressUpdateTimeRefresh()
            => Begin(_suppressUpdateTimeRefresh);

        /// <summary>进入"不记录墓碑"的作用域，<c>using</c> 结束后自动恢复。</summary>
        public static IDisposable BeginSuppressGraveRecording()
            => Begin(_suppressGraveRecording);

        private static IDisposable Begin(AsyncLocal<bool> flag)
        {
            bool previous = flag.Value;
            flag.Value = true;
            return new RestoreScope(() => flag.Value = previous);
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly Action _restore;

            public RestoreScope(Action restore)
            {
                _restore = restore;
            }

            public void Dispose()
            {
                _restore();
            }
        }
    }
}
