namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 复盘正文里的「受管区块」。芯片点选（情绪 / 类别 / 肯定）的产物落在这里，
    /// <b>区块外用户手写的段落原样保留</b>；区块不存在就追加到末尾。
    /// <para>
    /// 之所以只在保存时生成、加载时剥离，而不是让芯片实时改写编辑器：
    /// 项目用的 <c>MarkdownEdit</c> 只暴露了取值（GetValueAsync）与插入（InsertValueAsync），
    /// 没有整段设值的能力。因此「点选写回正文」的落点就是这里。
    /// </para>
    /// </summary>
    public static class ReviewContentBlock
    {
        public const string StartMarker = "<!-- review:facets:start -->";

        public const string EndMarker = "<!-- review:facets:end -->";

        /// <summary>剥掉正文里的受管区块，只留用户手写的内容。没有区块就原样返回。</summary>
        public static string Strip(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            int start = content.IndexOf(StartMarker, StringComparison.Ordinal);
            int end = content.IndexOf(EndMarker, StringComparison.Ordinal);
            if (start < 0 || end < start)
            {
                return content;
            }

            return content.Remove(start, end + EndMarker.Length - start).TrimEnd();
        }

        /// <summary>
        /// 把 <paramref name="block"/> 写进受管区块。先剥离旧区块，所以反复保存是幂等的，
        /// 不会一层层堆积。
        /// </summary>
        public static string Apply(string? content, string? block)
        {
            string body = Strip(content).TrimEnd();
            if (string.IsNullOrWhiteSpace(block))
            {
                return body;
            }

            string managed = $"{StartMarker}\n{block.Trim()}\n{EndMarker}";
            return body.Length == 0 ? managed : $"{body}\n\n{managed}";
        }
    }
}
