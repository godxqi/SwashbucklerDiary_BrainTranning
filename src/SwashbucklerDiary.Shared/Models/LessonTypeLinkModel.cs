namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 一条课题和它的某个类型。多选类型拆成多行，按类型计数直接分组。
    /// </summary>
    public class LessonTypeLinkModel : BaseModel
    {
        public Guid LessonId { get; set; }

        public LessonType Type { get; set; }
    }
}
