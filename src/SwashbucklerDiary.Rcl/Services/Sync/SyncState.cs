namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 本机的同步状态。整份以 JSON 存在 <c>IPreferences</c> 的 <see cref="SyncKeys.StateKey"/> 里。
    /// </summary>
    public class SyncState
    {
        /// <summary>首次同步时生成，用于在冲突副本上标注来源设备。</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 上次<b>成功</b>同步<b>开始</b>时的本地时间。
        /// <para>
        /// 取"开始"而不是"结束"：同步过程中用户新写的日记会落到下一次同步，
        /// 既不会漏，也不会因为时间落在窗口内而被重复推送。
        /// </para>
        /// </summary>
        public DateTime LastSessionStart { get; set; }

        /// <summary>上次成功同步时远端 manifest 的内容哈希，用于快速判断"什么都没变"。</summary>
        public string LastManifestHash { get; set; } = string.Empty;

        public DateTime? LastSuccessTime { get; set; }

        /// <summary>本机设备名，仅用于展示与冲突副本标注。</summary>
        public string DeviceName { get; set; } = string.Empty;
    }
}
