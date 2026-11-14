namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 自我肯定库（方法文档 4）。3–4 条短语，每天多次用心重复。
    /// 两个入口：月课 3.1 第三列的反对性陈述、3.4 敌人表的打击手段①。
    /// </summary>
    public class SelfAffirmationModel : BaseModel
    {
        public string? Content { get; set; }

        /// <summary>来自哪。书里示范了灵感可以来自书籍 / 电影 / 交谈 / 自己的感悟。</summary>
        public string? Source { get; set; }

        /// <summary>
        /// 由哪一次月课产出。非空表示它来自 3.2（3.1 第三列的反对性陈述）或
        /// 3.4 敌人表的打击手段①。
        /// <para>
        /// 这一列是 3.2 那一步的<b>闸门依据</b>：文档要求「每步要有产出才放行」，
        /// 而「本次新增了几条」只靠 <see cref="Source"/> 那段自由文本是查不出来的。
        /// 顺带把文档 4.2 那张表的「来自哪」列变成了可追溯的真实来源。
        /// </para>
        /// </summary>
        public Guid? SourceMonthlyReviewId { get; set; }

        /// <summary>来自哪篇课题（课题存档的归档块）。空表示不是从课题来的。</summary>
        public Guid? SourceLessonId { get; set; }

        /// <summary>用了几次。复盘主页顶部条点一下就 +1。</summary>
        public int RepeatCount { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
