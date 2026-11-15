using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 同步对象的序列化与内容哈希。
    /// <para>
    /// 哈希必须<b>跨设备稳定</b>：同一篇日记在 Android 和 Windows 上算出来的值要一样，
    /// 否则每次同步都会误判"内容变了"，反复上传同一份数据。
    /// </para>
    /// </summary>
    public interface ISyncObjectCodec
    {
        byte[] SerializeDiary(DiaryModel diary);

        DiaryModel? DeserializeDiary(byte[] data);

        byte[] SerializeReview(ReviewExportModel model);

        ReviewExportModel? DeserializeReview(byte[] data);

        byte[] SerializeLocations(List<LocationModel> locations);

        List<LocationModel>? DeserializeLocations(byte[] data);

        /// <summary>SHA256 小写十六进制。</summary>
        string Hash(byte[] data);
    }
}
