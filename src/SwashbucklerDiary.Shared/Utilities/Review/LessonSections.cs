using System.Text;

namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 课题记录的八个章节。用 4.x 当锚点，避开日课/周课/月课的 1/2/3。
    /// <para>
    /// 正文（<see cref="DiaryModel.Content"/>）是唯一真源：表单把这些字段拼成 Markdown，
    /// 再从这里解析回来。人物和课题类型也写进正文，导出的 Markdown 才能独立读懂；
    /// 人物标签仍然是「按人筛选」的索引，两者各司其职。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 各节的思想来源（只是解释框架，不是原典逐字引用）：
    /// <list type="bullet">
    /// <item>4.4 我的反应四层（身体 / 想法 / 情绪 / 意义感）：项目方法文档
    /// （Tara Swart《大脑训练手册》）；身体层与躯体心理学
    /// （van der Kolk《身体从未忘记》, 2014）同源。</item>
    /// <item>4.5 照见：荣格的「投射 / 阴影」；库利的「镜中我」(1902)；
    /// 「外面没有别人」是张德芬与《零极限》的通俗化说法，不是荣格原话。</item>
    /// <item>4.6 边界：阿德勒「课题分离」；Cloud 与 Townsend《Boundaries》(1992)。</item>
    /// <item>4.7 带走一句：项目方法文档 4 的自我肯定库；
    /// 自我肯定理论（Steele, 1988）与 CBT 的认知重构是邻近机制。</item>
    /// <item>4.8 下一步：行为激活（CBT）；Gollwitzer 的「执行意图」(1999)；
    /// SMART 目标（Doran, 1981）。</item>
    /// </list>
    /// </remarks>
    public sealed record LessonSections
    {
        public const int SectionGroup = 4;

        public const int NumberOfSections = 8;

        public const int ReactionCount = 4;

        /// <summary>4.1 人物。和人物标签重复，但导出后仍能读到。</summary>
        public string Person { get; set; } = string.Empty;

        /// <summary>4.2 课题类型。选中项用「、」连起来。</summary>
        public string LessonTypes { get; set; } = string.Empty;

        /// <summary>4.3 事件。</summary>
        public string Event { get; set; } = string.Empty;

        /// <summary>4.4 我的反应，顺序是身体 / 想法 / 情绪 / 意义感。</summary>
        public string[] Reactions { get; set; } = ["", "", "", ""];

        /// <summary>4.5 照见。</summary>
        public string Mirror { get; set; } = string.Empty;

        /// <summary>4.6 边界。</summary>
        public string Boundary { get; set; } = string.Empty;

        /// <summary>4.7 带走一句。</summary>
        public string Takeaway { get; set; } = string.Empty;

        /// <summary>4.8 下一步。</summary>
        public string NextStep { get; set; } = string.Empty;

        public bool IsEmpty
            => string.IsNullOrWhiteSpace(Person)
            && string.IsNullOrWhiteSpace(LessonTypes)
            && string.IsNullOrWhiteSpace(Event)
            && Reactions.All(string.IsNullOrWhiteSpace)
            && string.IsNullOrWhiteSpace(Mirror)
            && string.IsNullOrWhiteSpace(Boundary)
            && string.IsNullOrWhiteSpace(Takeaway)
            && string.IsNullOrWhiteSpace(NextStep);

        public static LessonSections Parse(string? content)
        {
            var bodies = ReviewSections.Split(content, SectionGroup, NumberOfSections);

            return new LessonSections
            {
                Person = bodies[0],
                LessonTypes = bodies[1],
                Event = bodies[2],
                Reactions = ReviewSections.ParseItems(bodies[3], ReactionCount),
                Mirror = bodies[4],
                Boundary = bodies[5],
                Takeaway = bodies[6],
                NextStep = bodies[7],
            };
        }

        /// <summary>
        /// 渲染回正文。<paramref name="titles"/> 依次对应 4.1 ~ 4.8 的标题；
        /// <paramref name="reactionLabels"/> 是四层反应的题干（身体 / 想法 / 情绪 / 意义感）。
        /// </summary>
        public string Compose(string?[] titles, string?[] reactionLabels)
        {
            var builder = new StringBuilder();

            AppendParagraph(builder, 1, titles.ElementAtOrDefault(0), Person);
            AppendParagraph(builder, 2, titles.ElementAtOrDefault(1), LessonTypes);
            AppendParagraph(builder, 3, titles.ElementAtOrDefault(2), Event);
            AppendItems(builder, 4, titles.ElementAtOrDefault(3), Reactions, reactionLabels);
            AppendParagraph(builder, 5, titles.ElementAtOrDefault(4), Mirror);
            AppendParagraph(builder, 6, titles.ElementAtOrDefault(5), Boundary);
            AppendParagraph(builder, 7, titles.ElementAtOrDefault(6), Takeaway);
            AppendParagraph(builder, 8, titles.ElementAtOrDefault(7), NextStep);

            return builder.ToString();
        }

        private static void AppendParagraph(StringBuilder builder, int number, string? title, string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, number, title);
            builder.Append('\n').Append(body.Trim());
        }

        private static void AppendItems(StringBuilder builder, int number, string? title, string[] items, string?[] labels)
        {
            if (items.All(string.IsNullOrWhiteSpace))
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, number, title);

            for (int i = 0; i < items.Length; i++)
            {
                builder.Append("\n- ");

                string? label = labels.ElementAtOrDefault(i);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    builder.Append("**").Append(label.Trim()).Append("** ");
                }

                // 列表项内部的换行要缩进，否则 Markdown 会当成新的一段
                builder.Append((items[i] ?? string.Empty).Trim().Replace("\n", "\n  "));
            }
        }
    }
}
