namespace SwashbucklerDiary.Shared
{
    public class TagModel : BaseModel
    {
        public string? Name { get; set; }

        /// <summary>
        /// 标签的用途：普通 / 触因 / 人物 / 课题。null 等同于 <see cref="TagKind.Normal"/>。
        /// 触因复用了整条标签管线，所以这里只加一个分类维度，不另建表。
        /// 做成可空的原因同 <see cref="DiaryModel.Kind"/>。
        /// </summary>
        public TagKind? Kind { get; set; }

        public List<DiaryModel>? Diaries { get; set; }
    }
}
