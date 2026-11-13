namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 周课 2.1「数频率」的数据源。日课录一条微观失败就自动落一行，
    /// 周课把它们列出来，<b>人</b>归类（<see cref="Category"/>）、<b>人</b>判性（<see cref="Verdict"/>），
    /// 机器只负责把同一类的次数数出来。机器算数，人做判断。
    /// <para>
    /// <see cref="Category"/> 由周课人工归出来（日课不再记类别），
    /// 所以字段是自由文本，候选列表从历史值里长出来。
    /// </para>
    /// </summary>
    public class StumblingBlockModel : BaseModel
    {
        public int Year { get; set; }

        public int WeekOfYear { get; set; }

        /// <summary>周课人工归的类。同一类 ≥2 次就是绊脚石候选；≥3 次且带情绪可能是印记，交给触因。</summary>
        public string? Category { get; set; }

        /// <summary>「发生了什么？」那一问的答案，也就是周课 2.1 要列出来的那一条。</summary>
        public string? Description { get; set; }

        /// <summary>来源日课日记。</summary>
        public Guid? SourceDiaryId { get; set; }

        /// <summary>
        /// 这条失败在当天日记里的序号（0 起算，对应正文 1.4 的 <c>### 1</c> / <c>### 2</c> 分块）。
        /// 它是增量同步的稳定键：<b>不能</b>靠「按 SourceDiaryId 查出来再按创建时间排序」推断序号——
        /// 用户删掉第 1 条之后第 2 条会顶上来，而周课已经判过的 <see cref="Verdict"/>
        /// 就会贴到错误的那条失败上。
        /// </summary>
        public int OrderIndex { get; set; }

        public BlockVerdict Verdict { get; set; }
    }
}
