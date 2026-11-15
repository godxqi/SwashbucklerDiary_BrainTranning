using SwashbucklerDiary.Shared;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncObjectCodec : ISyncObjectCodec
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            // 用源生成上下文，避免 Android PublishTrimmed 下反射序列化失败。
            TypeInfoResolver = SyncJsonContext.Default,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public byte[] SerializeDiary(DiaryModel diary)
            => JsonSerializer.SerializeToUtf8Bytes(Normalize(diary), _options);

        public DiaryModel? DeserializeDiary(byte[] data)
            => JsonSerializer.Deserialize<DiaryModel>(data, _options);

        public byte[] SerializeReview(ReviewExportModel model)
            => JsonSerializer.SerializeToUtf8Bytes(NormalizeReview(model), _options);

        public ReviewExportModel? DeserializeReview(byte[] data)
            => JsonSerializer.Deserialize<ReviewExportModel>(data, _options);

        public byte[] SerializeLocations(List<LocationModel> locations)
            => JsonSerializer.SerializeToUtf8Bytes(
                locations.OrderBy(it => it.Id).ToList(),
                _options);

        public List<LocationModel>? DeserializeLocations(byte[] data)
            => JsonSerializer.Deserialize<List<LocationModel>>(data, _options);

        public string Hash(byte[] data)
            => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

        /// <summary>
        /// 每张表按 Id 排序，保证同一份数据在两端序列化出相同字节。
        /// </summary>
        private static ReviewExportModel NormalizeReview(ReviewExportModel model) => new()
        {
            DailyReviews = model.DailyReviews.OrderBy(it => it.Id).ToList(),
            WeeklyReviews = model.WeeklyReviews.OrderBy(it => it.Id).ToList(),
            WeeklyGoals = model.WeeklyGoals.OrderBy(it => it.Id).ToList(),
            StumblingBlocks = model.StumblingBlocks.OrderBy(it => it.Id).ToList(),
            MonthlyReviews = model.MonthlyReviews.OrderBy(it => it.Id).ToList(),
            LimitingBeliefs = model.LimitingBeliefs.OrderBy(it => it.Id).ToList(),
            Enemies = model.Enemies.OrderBy(it => it.Id).ToList(),
            VirtueEntries = model.VirtueEntries.OrderBy(it => it.Id).ToList(),
            Affirmations = model.Affirmations.OrderBy(it => it.Id).ToList(),
            ArchiveItems = model.ArchiveItems.OrderBy(it => it.Id).ToList(),
            MacroGoals = model.MacroGoals.OrderBy(it => it.Id).ToList(),
            StateRecords = model.StateRecords.OrderBy(it => it.Id).ToList(),
            Lessons = model.Lessons.OrderBy(it => it.Id).ToList(),
            LessonTypeLinks = model.LessonTypeLinks.OrderBy(it => it.Id).ToList(),
            DailyEvents = model.DailyEvents.OrderBy(it => it.Id).ToList(),
        };

        /// <summary>
        /// 生成与"这一篇日记的内容"一一对应的规范化副本。
        /// <list type="number">
        /// <item>标签按 Id 排序、资源按 URI 排序——SqlSugar 的关联查询返回顺序不保证跨设备一致；</item>
        /// <item>丢掉子对象上的反向导航属性（<c>Tag.Diaries</c> / <c>Resource.Diaries</c>），否则会循环引用。</item>
        /// </list>
        /// </summary>
        private static DiaryModel Normalize(DiaryModel diary)
        {
#pragma warning disable CS0618 // Private 已过时，但历史数据里仍可能有值，同步时必须带上
            return new DiaryModel
            {
                Id = diary.Id,
                CreateTime = diary.CreateTime,
                UpdateTime = diary.UpdateTime,
                Title = diary.Title,
                Content = diary.Content,
                Mood = diary.Mood,
                Weather = diary.Weather,
                Location = diary.Location,
                Top = diary.Top,
                Private = diary.Private,
                Template = diary.Template,
                Kind = diary.Kind,
                Tags = diary.Tags?
                    .OrderBy(it => it.Id)
                    .Select(it => new TagModel
                    {
                        Id = it.Id,
                        CreateTime = it.CreateTime,
                        UpdateTime = it.UpdateTime,
                        Name = it.Name,
                        Kind = it.Kind,
                    })
                    .ToList(),
                Resources = diary.Resources?
                    .OrderBy(it => it.ResourceUri, StringComparer.Ordinal)
                    .Select(it => new ResourceModel
                    {
                        ResourceUri = it.ResourceUri,
                        ResourceType = it.ResourceType,
                    })
                    .ToList(),
            };
#pragma warning restore CS0618
        }
    }
}
