using System.Text.Json;

namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// <see cref="DiaryModel.Kind"/> 与 <see cref="TagModel.Kind"/> 都是**可空**列：
    /// 给已有表补列时 SQLite 不接受没有默认值的 NOT NULL 列，所以老数据读出来是 null。
    /// 而 SQL 里 null 不等于任何值——<c>WHERE Kind = 0</c> 会静默漏掉全部旧日记。
    /// 语义在这里收口：判断「是不是自由日记 / 是不是普通标签」一律走这两个方法，
    /// 新增的 <see cref="TagKind"/> 过滤也要记得同时接受 null。
    /// </summary>
    public static class ReviewModelExtensions
    {
        /// <summary>null 等同于 <see cref="ReviewKind.Free"/>。</summary>
        public static ReviewKind EffectiveKind(this DiaryModel diary)
        {
            return diary.Kind ?? ReviewKind.Free;
        }

        /// <summary>没有 Kind 的老日记就是自由日记。</summary>
        public static bool IsFree(this DiaryModel diary)
        {
            return diary.EffectiveKind() == ReviewKind.Free;
        }

        /// <summary>
        /// 日课 1.7 选取的自我肯定。<see cref="DailyReviewModel.AffirmationIds"/> 在库里是
        /// JSON 字符串，序列化只经由这一对方法，避免各调用点各写一套。
        /// </summary>
        public static List<Guid> GetAffirmationIds(this DailyReviewModel dailyReview)
        {
            if (string.IsNullOrEmpty(dailyReview.AffirmationIds))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<Guid>>(dailyReview.AffirmationIds) ?? [];
            }
            catch (JsonException)
            {
                //列被外部改坏时退化成"没选过"，不让一条脏数据把整个日课页带崩
                return [];
            }
        }

        public static void SetAffirmationIds(this DailyReviewModel dailyReview, IEnumerable<Guid>? affirmationIds)
        {
            var ids = affirmationIds?.ToList() ?? [];
            dailyReview.AffirmationIds = ids.Count == 0 ? null : JsonSerializer.Serialize(ids);
        }

        /// <summary>
        /// 只填了时间与触因、别的格子一个都没动——触因页「＋ 记一次」的产物，
        /// 等着用户去状态管理表的完整表单里补全。
        /// <para>
        /// 判据收在这里是因为有三个调用方，各写一份迟早会走样：
        /// <c>TriggerService.DeleteAsync</c> 靠它区分「空壳可以整行删掉」与
        /// 「写过内容，只把挂靠摘掉」，状态管理表列表靠它标「待补全」，
        /// 保存时靠它挡「整张表一个字都没填」。
        /// </para>
        /// </summary>
        public static bool IsTriggerOnly(this StateRecordModel record)
        {
            return record.TriggerTagId is not null && record.HasNothingWritten();
        }

        /// <summary>整张表一个字都没填，连触因都没选——这种不该落库。</summary>
        public static bool IsEmpty(this StateRecordModel record)
        {
            return record.TriggerTagId is null && record.HasNothingWritten();
        }

        /// <summary>
        /// 六格文字 + 情绪名 + 「调用哪一条」全都是空的。
        /// 「只有触因」与「整张表都空」只差触因那一项，共用这一份判断。
        /// </summary>
        private static bool HasNothingWritten(this StateRecordModel record)
        {
            return string.IsNullOrWhiteSpace(record.Scene)
                && string.IsNullOrWhiteSpace(record.Body)
                && string.IsNullOrWhiteSpace(record.Mind)
                && string.IsNullOrWhiteSpace(record.Emotion)
                && string.IsNullOrWhiteSpace(record.Spirit)
                && string.IsNullOrWhiteSpace(record.ActionNow)
                && record.EmotionName is null
                && record.InvokedKind is null
                && record.InvokedId is null;
        }
    }
}
