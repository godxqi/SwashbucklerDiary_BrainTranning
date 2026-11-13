using System.Text;

namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 月课摘要里那些要跟着界面语言走的栏目标签（三列审视的三列、敌人表的四列）。
    /// 由调用方从 i18n 取好再传进来，<see cref="MonthlyReviewSections"/> 本身不认识语言。
    /// </summary>
    public sealed record MonthlyReviewLabels(
        string? Review31,
        string? Review32,
        string? Review33,
        string? Review34,
        string? SupportingEvidence,
        string? CounterEvidence,
        string? Utility,
        string? EnemyGoal,
        string? EnemyBlockingBehavior,
        string? EnemyDeepBelief,
        string? EnemyWeeklyAction);

    /// <summary>
    /// 月课（方法文档 3）日记正文的<b>生成式摘要</b>。
    /// <para>
    /// 与日课 / 周课不同：月课的产出<b>全是结构化的</b>（三列审视六行、肯定短语、敌人表三行），
    /// 没有用户手写的散文，所以这里<b>只有渲染、没有解析</b>——表里的数据才是真源。
    /// 摘要只为了让这篇日记在日历、搜索、纯 Markdown 导出里也读得懂
    /// （这就是「存储走混合：结构化进表 + 散文进 Content」那条决策在月课上的落法）。
    /// </para>
    /// <para>
    /// 渲染结果由服务写进 <see cref="ReviewContentBlock"/>，所以每次保存都重新生成；
    /// 区块之外用户后来在「库表」里补写的内容不受影响。
    /// </para>
    /// </summary>
    public static class MonthlyReviewSections
    {
        /// <summary>章节组号：日课 1.x、周课 2.x、月课 3.x。</summary>
        public const int SectionGroup = 3;

        /// <summary>3.1 ~ 3.4 四节。</summary>
        public const int MaxSection = 4;

        /// <summary>3.1 三列审视最多六条（文档原文「最多六种」）。</summary>
        public const int MaxBeliefs = 6;

        /// <summary>3.4 敌人表是三列三行。</summary>
        public const int MaxEnemies = 3;

        /// <summary>3.2 的短语数量建议。文档原文「我通常觉得三四个就足够了」。</summary>
        public const int SuggestedAffirmationCount = 4;

        public static string Compose(MonthlyReviewLabels labels,
            IReadOnlyList<LimitingBeliefModel> beliefs,
            IReadOnlyList<SelfAffirmationModel> affirmations,
            string? step33Note,
            IReadOnlyList<EnemyModel> enemies)
        {
            var builder = new StringBuilder();

            AppendBeliefs(builder, labels, beliefs);
            AppendAffirmations(builder, labels, affirmations);
            AppendNote(builder, labels, step33Note);
            AppendEnemies(builder, labels, enemies);

            return builder.ToString();
        }

        /// <summary>摘要里是不是一个字都没有。用来判断「这次月课什么都没做」。</summary>
        public static bool IsEmpty(string? digest) => string.IsNullOrWhiteSpace(digest);

        private static void AppendBeliefs(StringBuilder builder,
            MonthlyReviewLabels labels,
            IReadOnlyList<LimitingBeliefModel> beliefs)
        {
            var rows = beliefs.Where(it => !string.IsNullOrWhiteSpace(it.Assertion)).ToList();
            if (rows.Count == 0)
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, 1, labels.Review31);

            for (int i = 0; i < rows.Count; i++)
            {
                //编号用粗体而不是 ###：日课的 1.4 用 ### 编号标微观失败，
                //这里换个记号，免得以后有人把两种分块混在一起解析
                builder.Append("\n\n**").Append(i + 1).Append(".** ").Append(rows[i].Assertion!.Trim());
                AppendBullet(builder, labels.SupportingEvidence, rows[i].SupportingEvidence);
                AppendBullet(builder, labels.CounterEvidence, rows[i].CounterEvidence);
                AppendBullet(builder, labels.Utility, rows[i].Utility);
            }
        }

        private static void AppendAffirmations(StringBuilder builder,
            MonthlyReviewLabels labels,
            IReadOnlyList<SelfAffirmationModel> affirmations)
        {
            var rows = affirmations
                .Where(it => !string.IsNullOrWhiteSpace(it.Content))
                .ToList();
            if (rows.Count == 0)
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, 2, labels.Review32);

            foreach (var item in rows)
            {
                builder.Append("\n- ").Append(item.Content!.Trim());
            }
        }

        private static void AppendNote(StringBuilder builder, MonthlyReviewLabels labels, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, 3, labels.Review33);
            builder.Append('\n').Append(note.Trim());
        }

        private static void AppendEnemies(StringBuilder builder,
            MonthlyReviewLabels labels,
            IReadOnlyList<EnemyModel> enemies)
        {
            var rows = enemies.Where(HasAnyColumn).ToList();
            if (rows.Count == 0)
            {
                return;
            }

            ReviewSections.AppendHeading(builder, SectionGroup, 4, labels.Review34);

            builder.Append("\n| ")
                .Append(Cell(labels.EnemyGoal)).Append(" | ")
                .Append(Cell(labels.EnemyBlockingBehavior)).Append(" | ")
                .Append(Cell(labels.EnemyDeepBelief)).Append(" | ")
                .Append(Cell(labels.EnemyWeeklyAction)).Append(" |");

            builder.Append("\n| --- | --- | --- | --- |");

            foreach (var row in rows)
            {
                builder.Append("\n| ")
                    .Append(Cell(row.Goal)).Append(" | ")
                    .Append(Cell(row.BlockingBehavior)).Append(" | ")
                    .Append(Cell(row.DeepBelief)).Append(" | ")
                    .Append(Cell(row.WeeklyAction)).Append(" |");
            }
        }

        /// <summary>敌人表里四格全空的行不渲染——空行进 Markdown 表只是噪音。</summary>
        private static bool HasAnyColumn(EnemyModel enemy)
        {
            return !string.IsNullOrWhiteSpace(enemy.Goal)
                || !string.IsNullOrWhiteSpace(enemy.BlockingBehavior)
                || !string.IsNullOrWhiteSpace(enemy.DeepBelief)
                || !string.IsNullOrWhiteSpace(enemy.WeeklyAction);
        }

        private static void AppendBullet(StringBuilder builder, string? label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.Append("\n- ");
            if (!string.IsNullOrWhiteSpace(label))
            {
                builder.Append("**").Append(label.Trim()).Append("** ");
            }

            //列表项里的换行要缩进，否则 Markdown 会当成新的一段
            builder.Append(value.Trim().Replace("\n", "\n  "));
        }

        /// <summary>表格单元格：竖线要转义，换行要变成 &lt;br&gt;，否则表会散架。</summary>
        private static string Cell(string? value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace("|", "\\|")
                .Replace("\r\n", "<br>")
                .Replace("\n", "<br>");
        }
    }
}
