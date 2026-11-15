using SwashbucklerDiary.Shared;
using System.Text.Json.Serialization;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 同步相关 JSON 的源生成序列化上下文。
    /// <para>
    /// Android Release 开启了 <c>PublishTrimmed</c>，反射式 <c>JsonSerializer</c>
    /// 在裁剪后可能抛 <c>NotSupportedException</c>。新增同步模型时记得在这里登记，
    /// 否则真机上会出现"桌面端正常、手机上同步状态读不出来"的怪问题。
    /// </para>
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(SyncState))]
    [JsonSerializable(typeof(SyncManifest))]
    [JsonSerializable(typeof(DiaryModel))]
    [JsonSerializable(typeof(List<DiaryModel>))]
    [JsonSerializable(typeof(TagModel))]
    [JsonSerializable(typeof(List<TagModel>))]
    [JsonSerializable(typeof(ResourceModel))]
    [JsonSerializable(typeof(List<ResourceModel>))]
    [JsonSerializable(typeof(LocationModel))]
    [JsonSerializable(typeof(List<LocationModel>))]
    [JsonSerializable(typeof(ReviewExportModel))]
    [JsonSerializable(typeof(List<DailyReviewModel>))]
    [JsonSerializable(typeof(List<WeeklyReviewModel>))]
    [JsonSerializable(typeof(List<WeeklyGoalModel>))]
    [JsonSerializable(typeof(List<StumblingBlockModel>))]
    [JsonSerializable(typeof(List<MonthlyReviewModel>))]
    [JsonSerializable(typeof(List<LimitingBeliefModel>))]
    [JsonSerializable(typeof(List<EnemyModel>))]
    [JsonSerializable(typeof(List<VirtueEntryModel>))]
    [JsonSerializable(typeof(List<SelfAffirmationModel>))]
    [JsonSerializable(typeof(List<ArchiveItemModel>))]
    [JsonSerializable(typeof(List<MacroGoalModel>))]
    [JsonSerializable(typeof(List<StateRecordModel>))]
    [JsonSerializable(typeof(List<LessonModel>))]
    [JsonSerializable(typeof(List<LessonTypeLinkModel>))]
    [JsonSerializable(typeof(List<DailyEventModel>))]
    internal partial class SyncJsonContext : JsonSerializerContext
    {
    }
}
