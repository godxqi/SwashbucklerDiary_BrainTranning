using System.Text.Json.Serialization;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 远端 <c>Sync/manifest.json</c>。它是同步唯一的提交点：
    /// 内容文件先写、最后 CAS 提交 manifest，中断只会留下孤儿文件，不会留下半坏索引。
    /// </summary>
    public sealed class SyncManifest
    {
        public const int CurrentSchema = 1;

        [JsonPropertyName("schema")]
        public int Schema { get; set; } = CurrentSchema;

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = string.Empty;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("deviceId")]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>日记：key 为日记 Id（<c>guid "N"</c>）。</summary>
        [JsonPropertyName("diaries")]
        public Dictionary<string, SyncObjectMeta> Diaries { get; set; } = [];

        /// <summary>复盘表：key 为表短名（<c>daily</c>、<c>weekly</c>…），整表一个文件。</summary>
        [JsonPropertyName("review")]
        public Dictionary<string, SyncObjectMeta> Review { get; set; } = [];

        /// <summary>地点字典，单文件。</summary>
        [JsonPropertyName("locations")]
        public SyncObjectMeta? Locations { get; set; }

        /// <summary>附件：key 为 <c>{类型}/{文件名}</c>，文件名是内容 SHA256。</summary>
        [JsonPropertyName("assets")]
        public Dictionary<string, SyncObjectMeta> Assets { get; set; } = [];

        /// <summary>墓碑：key 为带类型前缀的统一键，值为删除时间（Unix ms）。</summary>
        [JsonPropertyName("graves")]
        public Dictionary<string, long> Graves { get; set; } = [];
    }

    /// <summary>
    /// 单个同步对象的元数据。
    /// <para>
    /// <see cref="UpdateTime"/> 用对象自身的 <c>UpdateTime</c>，不用 WebDAV 的服务器时间——
    /// 坚果云的 GET/PROPFIND 不返回可靠的 Last-Modified，而且两端时钟可能不一致。
    /// </para>
    /// </summary>
    public sealed record SyncObjectMeta
    {
        [JsonPropertyName("u")]
        public long UpdateTime { get; init; }

        [JsonPropertyName("h")]
        public string Hash { get; init; } = string.Empty;
    }
}
