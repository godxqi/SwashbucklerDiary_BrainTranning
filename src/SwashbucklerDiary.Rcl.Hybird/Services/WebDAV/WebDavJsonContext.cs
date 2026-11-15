using System.Text.Json.Serialization;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// WebDAV 配置表单的源生成序列化上下文。
    /// Android Release 开了 PublishTrimmed，反射反序列化可能在真机上失败。
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(WebDavConfigForm))]
    internal partial class WebDavJsonContext : JsonSerializerContext
    {
    }
}
