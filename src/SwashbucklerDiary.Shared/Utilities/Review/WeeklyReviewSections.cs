using System.Text;

namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 周课（方法文档 2）里<b>散文</b>的那几节：2.1 的结算追问、2.4 四个记录表检查、2.5 本周一件新事物。
    /// <para>
    /// 顺序是 2026-10-06 主人定的（<b>先结算上周做完的事，再回头数、提炼，最后定下周</b>）：
    /// 2.1 本周三个目标结算 / 2.2 数微观失败频率 / 2.3 微观失败提炼 /
    /// 2.4 四个记录表检查 / 2.5 本周一件新事物 / 2.6 下周三个目标。
    /// </para>
    /// <para>
    /// 2.2 的判定、2.6 的目标卡、2.1 的结算状态都是结构化数据，存在表里，
    /// 只把可读的摘要渲染进正文末尾的受管区块——所以不在这里。
    /// 2.3「提炼」也不单独落库：选中的那一条会变成一个 2.6 的目标卡，目标卡本身就是记录。
    /// </para>
    /// <para>
    /// <b>章节号是正文里的锚点</b>，与页面上的显示顺序必须一致——解析和拼装都靠它。
    /// </para>
    /// </summary>
    public sealed record WeeklyReviewSections
    {
        /// <summary>正文里的章节组号：周课是 2.x。</summary>
        public const int SectionGroup = 2;

        /// <summary>2.4 四个记录表检查固定四个问题。</summary>
        public const int ExitQuestionCount = 4;

        /// <summary>
        /// 认到 2.6 为止（虽然 2.6 没有散文）。留这一格是为了别人手改正文时，
        /// 写在 <c>## 2.6</c> 下的字不会漏进 2.5 的正文里。
        /// </summary>
        private const int MaxSection = 6;

        /// <summary>2.1 末尾那一问：这一周有没有什么不一样的事，是因为这套系统而发生的。</summary>
        public string SettlementReflection { get; set; } = string.Empty;

        /// <summary>2.4 四个记录表检查，顺序与文档一致（状态管理表 / 四个 List / 三个优点表 / 自我肯定库）。</summary>
        public string[] Exits { get; set; } = ["", "", "", ""];

        /// <summary>2.5 本周一件新事物。</summary>
        public string NewThing { get; set; } = string.Empty;

        public bool IsEmpty
            => string.IsNullOrWhiteSpace(SettlementReflection)
            && Exits.All(string.IsNullOrWhiteSpace)
            && string.IsNullOrWhiteSpace(NewThing);

        public static WeeklyReviewSections Parse(string? content)
        {
            var bodies = ReviewSections.Split(content, SectionGroup, MaxSection);

            return new WeeklyReviewSections
            {
                SettlementReflection = bodies[0],
                Exits = ReviewSections.ParseItems(bodies[3], ExitQuestionCount),
                NewThing = bodies[4],
            };
        }

        /// <summary>
        /// 渲染回正文。<paramref name="titles"/> 依次对应 2.1 / 2.4 / 2.5 的标题，
        /// <paramref name="exitQuestions"/> 是 2.4 四个问题的题干（由调用方按当前语言给）。
        /// </summary>
        public string Compose(string?[] titles, string?[] exitQuestions)
        {
            var builder = new StringBuilder();

            AppendParagraph(builder, 1, titles.ElementAtOrDefault(0), SettlementReflection);
            AppendExits(builder, titles.ElementAtOrDefault(1), exitQuestions);
            AppendParagraph(builder, 5, titles.ElementAtOrDefault(2), NewThing);

            return builder.ToString();
        }

        private void AppendExits(StringBuilder builder, string? title, string?[] questions)
        {
            if (Exits.All(string.IsNullOrWhiteSpace))
            {
                return;
            }

            //2.4：四个记录表检查
            ReviewSections.AppendHeading(builder, SectionGroup, 4, title);

            for (int i = 0; i < Exits.Length; i++)
            {
                builder.Append("\n- ");

                string? label = questions.ElementAtOrDefault(i);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    builder.Append("**").Append(label.Trim()).Append("** ");
                }

                //列表项内部的换行要缩进，否则 Markdown 会当成新的一段
                builder.Append((Exits[i] ?? string.Empty).Trim().Replace("\n", "\n  "));
            }
        }

        private static void AppendParagraph(StringBuilder builder, int number, string? title, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, number, title);
            builder.Append('\n').Append(body.Trim());
        }

    }
}
