using System.Text;
using System.Text.RegularExpressions;

namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 复盘正文按<b>章节号</b>切分与拼装的公共部分。日课用 1.x、周课用 2.x、月课用 3.x。
    /// <para>
    /// 锚点必须是章节号而不是标题文字——标题跟着界面语言变，号码不变，
    /// 所以用户切了语言之后老日记照样解析得回来。
    /// </para>
    /// </summary>
    public static class ReviewSections
    {
        /// <summary>
        /// 把正文按 <c>## {group}.1</c> ~ <c>## {group}.N</c> 切成 <paramref name="count"/> 段原始正文。
        /// 没有章节标题时整段都算第一节——宁可归错节，也不能把用户写的东西丢掉。
        /// </summary>
        public static string[] Split(string? content, int group, int count)
        {
            var bodies = new string[count];
            if (string.IsNullOrWhiteSpace(content))
            {
                return bodies;
            }

            var matches = Regex.Matches(content,
                $@"^\s{{0,3}}#{{1,6}}\s*{group}\.([1-9])\b",
                RegexOptions.Multiline);

            if (matches.Count == 0)
            {
                bodies[0] = content.Trim();
                return bodies;
            }

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                int number = match.Groups[1].Value[0] - '0';
                if (number < 1 || number > count)
                {
                    continue;
                }

                //标题那一行剩下的文字（标题名）不算正文，从下一行开始
                int lineEnd = content.IndexOf('\n', match.Index + match.Length);
                int bodyStart = lineEnd < 0 ? content.Length : lineEnd + 1;
                int bodyEnd = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
                if (bodyEnd < bodyStart)
                {
                    bodyEnd = bodyStart;
                }

                bodies[number - 1] = content[bodyStart..bodyEnd].Trim();
            }

            return bodies;
        }

        /// <summary>
        /// 一个 Markdown 列表项。日课 1.3/1.4 与周课 2.5 都用它拆固定条数，
        /// 放在这里是为了别再各存一份 —— 原先日课有一份、周课还得写成
        /// <c>DailyReviewSections.ItemRegex</c> 去引用它。
        /// </summary>
        private static readonly Regex itemRegex = new(
            @"^\s{0,3}[-*]\s+(?<text>.*)$", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// 把一节拆成固定条数。列表记号之外的续行并到当前条里；
        /// 一条都没识别到就把整段当第一条 —— 同样是为了不丢字。
        /// <para>
        /// 日课 1.3（三项积极的）、日课 1.4（一组四问）、周课 2.5（四处出口）共用这一份。
        /// </para>
        /// </summary>
        public static string[] ParseItems(string body, int count)
        {
            var items = new string[count];
            Array.Fill(items, string.Empty);

            if (string.IsNullOrWhiteSpace(body))
            {
                return items;
            }

            string normalized = body.Replace("\r\n", "\n");
            var matches = itemRegex.Matches(normalized);

            if (matches.Count == 0)
            {
                items[0] = normalized.Trim();
                return items;
            }

            int index = -1;
            int consumedTo = 0;

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];

                //列表记号之间的零散文字，并到上一条
                if (index >= 0)
                {
                    string between = normalized[consumedTo..match.Index].Trim();
                    if (between.Length > 0)
                    {
                        items[index] = Join(items[index], between);
                    }
                }
                else if (normalized[..match.Index].Trim() is { Length: > 0 } prelude)
                {
                    index = 0;
                    items[0] = prelude;
                }

                //多出来的条目并到最后一条，不新建一栏
                index = Math.Min(index + 1, count - 1);
                items[index] = Join(items[index], StripLabel(match.Groups["text"].Value.Trim()));
                consumedTo = match.Index + match.Length;
            }

            string tail = normalized[consumedTo..].Trim();
            if (index >= 0 && tail.Length > 0)
            {
                items[index] = Join(items[index], tail);
            }

            return items;
        }

        /// <summary>去掉格子前面回显用的题干（<c>**题干**</c>）。</summary>
        private static string StripLabel(string text)
        {
            if (!text.StartsWith("**", StringComparison.Ordinal))
            {
                return text;
            }

            int end = text.IndexOf("**", 2, StringComparison.Ordinal);
            return end < 0 ? text : text[(end + 2)..].TrimStart();
        }

        private static string Join(string current, string addition)
            => current.Length == 0 ? addition : current + "\n" + addition;

        /// <summary>拼一个章节标题（形如 <c>## 2.5 四处出口检查</c>）。标题为空就只留编号。</summary>
        public static void AppendHeading(StringBuilder builder, int group, int number, string? title)
        {
            if (builder.Length > 0)
            {
                builder.Append("\n\n");
            }

            builder.Append("## ").Append(group).Append('.').Append(number);

            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.Append(' ').Append(title.Trim());
            }
        }
    }
}
