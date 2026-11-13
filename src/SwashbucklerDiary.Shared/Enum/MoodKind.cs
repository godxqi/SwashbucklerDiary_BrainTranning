namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 八大情绪（方法文档 1.6 / ch06）。
    /// 生存情绪：恐惧、愤怒、厌恶、羞耻、悲伤；
    /// 依恋情绪：喜悦·兴奋、爱·信任；增效剂：惊讶。
    /// 从 1 开始，0 视为未选择——用它的字段一律可空，别退化成默认值 0。
    /// 两个落点：<see cref="DailyReviewModel.Mood"/>（日课 1.6 情绪命名）与
    /// <see cref="StateRecordModel.EmotionName"/>（状态表 5.1「这条情绪叫什么名字」）。
    /// 注意 <see cref="DiaryModel.Mood"/> 是另一回事，那是旧版自由日记的字符串心情。
    /// </summary>
    public enum MoodKind
    {
        Fear = 1,
        Anger = 2,
        Disgust = 3,
        Shame = 4,
        Sadness = 5,
        Joy = 6,
        Love = 7,
        Surprise = 8,
    }
}
