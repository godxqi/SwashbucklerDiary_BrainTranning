using System.Text;
using System.Text.RegularExpressions;

namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 日课的六个散文章节（方法文档 1）：
    /// 1.1 生活上的 / 1.2 学习上的 / 1.3 今天做的三项积极的 / 1.4 微观失败 /
    /// 1.5 精力·分心·疲劳 / 1.6 情绪命名之外那一句「这个心情是被什么引起的」。
    /// <para>
    /// 用<b>章节号</b>（1.1 ~ 1.6）当锚点，不用标题文字——标题跟着界面语言变，号码不变，
    /// 所以用户切了语言之后老日记照样解析得回来。
    /// </para>
    /// <para>
    /// 1.3 固定三条；<b>1.4 是「一组四问」可重复</b>——四问是针对<b>一个</b>微观失败的，
    /// 一天可能不止一条，所以正文里每条失败用一个 <c>### 编号</c> 分块。
    /// </para>
    /// <para>
    /// <b>1.6 只放「原因」这一句散文</b>；那个八情绪芯片本身是结构化的
    /// （<c>DailyReviewModel.Mood</c>），渲染在正文末尾的受管区块里。
    /// 1.7 同理，只有芯片没有散文，所以不在这里。
    /// </para>
    /// <para>
    /// 正文（<see cref="DiaryModel.Content"/>）是唯一真源：本类型只负责在
    /// 「一节一段的正文」和「几个字段」之间来回转换。
    /// </para>
    /// </summary>
    public sealed record DailyReviewSections
    {
        /// <summary>正文里的章节组号：日课是 1.x。</summary>
        public const int SectionGroup = 1;

        /// <summary>1.1 ~ 1.6。老日记里没有 <c>## 1.6</c> 标题，解析出来就是个空串，不丢字。</summary>
        public const int NumberOfSections = 6;

        /// <summary>1.3 固定三条。</summary>
        public const int ThreePositivesCount = 3;

        /// <summary>1.4 每条微观失败固定四问。</summary>
        public const int MicroFailureQuestionCount = 4;

        /// <summary>1.4 内部每条微观失败的分块标题。三级以上，免得和 <c>## 1.x</c> 撞。</summary>
        private static readonly Regex subBlockRegex = new(
            @"^\s{0,3}#{3,6}\s*(?<title>.*)$", RegexOptions.Multiline | RegexOptions.Compiled);

        public string Life { get; set; } = string.Empty;

        public string Study { get; set; } = string.Empty;

        /// <summary>1.3 今天做的三项积极的。</summary>
        public string[] ThreePositives { get; set; } = ["", "", ""];

        /// <summary>1.4 的微观失败，每条形如 <c>[答1, 答2, 答3, 答4]</c>。至少一条。</summary>
        public List<string[]> MicroFailures { get; set; } = [NewMicroFailure()];

        public string EnergyNote { get; set; } = string.Empty;

        /// <summary>
        /// 1.6 那句原因：这个心情是被什么引起的。
        /// 光给情绪命名只完成了「情绪素养」那一半，写清引发它的东西才是下一次能认出来的线索
        /// （状态管理表 5.2 说的「触因不随机，它是相似感的指纹」用的是同一批记录）。
        /// </summary>
        public string MoodNote { get; set; } = string.Empty;

        public bool IsEmpty
            => string.IsNullOrWhiteSpace(Life)
            && string.IsNullOrWhiteSpace(Study)
            && ThreePositives.All(string.IsNullOrWhiteSpace)
            && MicroFailures.All(group => group.All(string.IsNullOrWhiteSpace))
            && string.IsNullOrWhiteSpace(EnergyNote)
            && string.IsNullOrWhiteSpace(MoodNote);

        /// <summary>一条空的微观失败（四个空答案）。</summary>
        public static string[] NewMicroFailure()
        {
            var answers = new string[MicroFailureQuestionCount];
            Array.Fill(answers, string.Empty);
            return answers;
        }

        /// <summary>
        /// 把正文切成六节。没有章节标题时整段都算 1.1——宁可归错节，也不能把用户写的东西丢掉。
        /// </summary>
        public static DailyReviewSections Parse(string? content)
        {
            var sections = new DailyReviewSections();
            var bodies = ParseSectionBodies(content);

            sections.Life = bodies[0];
            sections.Study = bodies[1];
            sections.ThreePositives = ReviewSections.ParseItems(bodies[2], ThreePositivesCount);
            sections.MicroFailures = ParseMicroFailures(bodies[3]);
            sections.EnergyNote = bodies[4];
            sections.MoodNote = bodies[5];

            return sections;
        }

        /// <summary>只按章节号切成六段原始正文（不拆列表项也不拆微观失败分块）。</summary>
        private static string[] ParseSectionBodies(string? content)
        {
            return ReviewSections.Split(content, SectionGroup, NumberOfSections);
        }

        /// <summary>
        /// 渲染回正文。<paramref name="titles"/> 依次对应 1.1 ~ 1.6 的标题名，
        /// <paramref name="microFailureQuestions"/> 是 1.4 四问的题干（由调用方按当前语言给）。
        /// </summary>
        public string Compose(string?[] titles, string?[] microFailureQuestions)
        {
            var builder = new StringBuilder();

            AppendParagraph(builder, 1, titles.ElementAtOrDefault(0), Life);
            AppendParagraph(builder, 2, titles.ElementAtOrDefault(1), Study);
            AppendItems(builder, 3, titles.ElementAtOrDefault(2), ThreePositives, null);
            AppendMicroFailures(builder, titles.ElementAtOrDefault(3), microFailureQuestions);
            AppendParagraph(builder, 5, titles.ElementAtOrDefault(4), EnergyNote);
            AppendParagraph(builder, 6, titles.ElementAtOrDefault(5), MoodNote);

            return builder.ToString();
        }

        private void AppendMicroFailures(StringBuilder builder, string? title, string?[] questions)
        {
            //全空的条目不落盘，免得留一堆空编号
            var groups = MicroFailures.Where(group => !group.All(string.IsNullOrWhiteSpace)).ToList();
            if (groups.Count == 0)
            {
                return;
            }

            AppendHeading(builder, 4, title);

            for (int i = 0; i < groups.Count; i++)
            {
                builder.Append($"\n\n### {i + 1}");

                for (int q = 0; q < groups[i].Length; q++)
                {
                    builder.Append("\n- ");

                    string? label = questions.ElementAtOrDefault(q);
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        builder.Append("**").Append(label.Trim()).Append("** ");
                    }

                    builder.Append((groups[i][q] ?? string.Empty).Trim().Replace("\n", "\n  "));
                }
            }
        }

        private static void AppendParagraph(StringBuilder builder, int number, string? title, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            AppendHeading(builder, number, title);
            builder.Append('\n').Append(body.Trim());
        }

        private static void AppendItems(StringBuilder builder, int number, string? title, string[] items, string?[]? labels)
        {
            if (items.All(string.IsNullOrWhiteSpace))
            {
                return;
            }

            AppendHeading(builder, number, title);

            for (int i = 0; i < items.Length; i++)
            {
                builder.Append("\n- ");

                string? label = labels?.ElementAtOrDefault(i);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    builder.Append("**").Append(label.Trim()).Append("** ");
                }

                //列表项内部的换行要缩进，否则 Markdown 会当成新的一段
                builder.Append((items[i] ?? string.Empty).Trim().Replace("\n", "\n  "));
            }
        }

        private static void AppendHeading(StringBuilder builder, int number, string? title)
        {
            ReviewSections.AppendHeading(builder, SectionGroup, number, title);
        }

        /// <summary>
        /// 1.4 拆成若干条。没有 <c>### 编号</c> 分块时整段算一条——老格式和手改过的正文都不丢字。
        /// </summary>
        private static List<string[]> ParseMicroFailures(string body)
        {
            var groups = SplitSubBlocks(body)
                .Select(block => ReviewSections.ParseItems(block, MicroFailureQuestionCount))
                .ToList();

            //末尾的空条目是渲染时被跳过的那些，去掉；但至少留一条空的给用户填
            while (groups.Count > 1 && groups[^1].All(string.IsNullOrWhiteSpace))
            {
                groups.RemoveAt(groups.Count - 1);
            }

            if (groups.Count == 0)
            {
                groups.Add(NewMicroFailure());
            }

            return groups;
        }

        private static List<string> SplitSubBlocks(string body)
        {
            List<string> blocks = [];
            if (string.IsNullOrWhiteSpace(body))
            {
                return blocks;
            }

            string normalized = body.Replace("\r\n", "\n");
            var matches = subBlockRegex.Matches(normalized);

            if (matches.Count == 0)
            {
                blocks.Add(normalized.Trim());
                return blocks;
            }

            if (normalized[..matches[0].Index].Trim() is { Length: > 0 } prelude)
            {
                blocks.Add(prelude);
            }

            for (int i = 0; i < matches.Count; i++)
            {
                int lineEnd = normalized.IndexOf('\n', matches[i].Index + matches[i].Length);
                int start = lineEnd < 0 ? normalized.Length : lineEnd + 1;
                int end = i + 1 < matches.Count ? matches[i + 1].Index : normalized.Length;
                blocks.Add(end <= start ? string.Empty : normalized[start..end].Trim());
            }

            return blocks;
        }

    }
}
