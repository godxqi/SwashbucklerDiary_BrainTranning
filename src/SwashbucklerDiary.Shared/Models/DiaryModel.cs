namespace SwashbucklerDiary.Shared
{
    public class DiaryModel : BaseModel
    {
        public string? Title { get; set; }

        public string? Content { get; set; }

        public string? Mood { get; set; }

        public string? Weather { get; set; }

        public string? Location { get; set; }

        public bool Top { get; set; }

        [Obsolete("Privacy mode no requires it after 1.3.9")]
        public bool Private { get; set; }

        /// <summary>
        /// It was added later, so it may be null in SQL.
        /// </summary>
        public bool Template { get; set; }

        /// <summary>
        /// 这条日记在复盘流水线里的身份（日课 / 周课 / 月课）。null 等同于 <see cref="ReviewKind.Free"/>。
        /// 做成可空是因为给已有表补列时 SQLite 不接受没有默认值的 NOT NULL 列，
        /// 非空会让老用户的库在升级时直接打不开。
        /// </summary>
        public ReviewKind? Kind { get; set; }

        public List<TagModel>? Tags { get; set; }

        public List<ResourceModel>? Resources { get; set; }
    }
}
