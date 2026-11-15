namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 同步墓碑：记录一次删除，供另一端传播。
    /// <para>
    /// 只记录<b>主库</b>的删除。隐私模式库首版不参与同步，所以隐私库里的删除不写墓碑，
    /// 否则会永远累积在本地却没有任何一端来消费。
    /// </para>
    /// </summary>
    public class SyncGraveModel : BaseModel
    {
        /// <summary>实体类别，如 <c>diary</c>、<c>tag</c>、<c>review:daily</c>、<c>asset</c>。</summary>
        public string EntityType { get; set; } = string.Empty;

        /// <summary>被删除实体的 Guid。附件等非 Guid 实体留 <see cref="Guid.Empty"/>。</summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// 统一键：<c>"{EntityType}:{EntityId:N}"</c>；附件用 <c>"asset:{类型}/{文件名}"</c>。
        /// <para>
        /// 单独存一份是为了让两端拼出来的键完全一致——各自拼字符串很容易在大小写、
        /// Guid 格式上产生分歧，分歧的后果是墓碑认不出来，数据"复活"。
        /// </para>
        /// </summary>
        public string EntityKey { get; set; } = string.Empty;

        public DateTime DeletedTime { get; set; } = DateTime.Now;
    }
}
