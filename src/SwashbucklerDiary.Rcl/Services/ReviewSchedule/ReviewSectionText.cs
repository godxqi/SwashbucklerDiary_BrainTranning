using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 日课与周课各节的标题、「该填什么」的说明，以及各节里的题干。
    /// 文案取自方法文档 1.1 ~ 1.7 与 2.4 ~ 2.6 的正文，集中在一处免得抄走样。
    /// <para>
    /// 两个地方要用它：页面（显示每节的说明与题干）、服务（把章节渲染进正文）。
    /// </para>
    /// <para>
    /// 这些英文句子同时就是 i18n 的 key。新增文案记得同步补语言包，
    /// 否则别的语言会显示成英文原句。
    /// </para>
    /// </summary>
    public static class ReviewSectionText
    {
        // 这些英文句子同时就是 i18n 的 key，和项目里其它文案一个路子。
        public const string LifeDescription = "Write down your thoughts and reactions to the events and people in your life. It doesn't need to be long, but be honest about your feelings, motives and behaviour.";

        public const string StudyDescription = "Record your thoughts and plans about studying.";

        public const string ThreePositivesDescription = "Write down three positive things you did today that help nurture your roots and create the future you want. They can be very small.";

        public const string MicroFailureDescription = "Micro failure intro";

        public const string EnergyDescription = "Note what gave you energy, and what left you distracted or drained.";

        public const string EmotionDescription = "Name the emotion. Holding some objectivity makes it far less likely to sweep you away.";

        public const string AffirmationDescription = "Pick one from your affirmation library and repeat it mindfully several times today. If none of them lands, just note \"not repeated\".";

        private static readonly string[] microFailureQuestionKeys =
        [
            "What happened?",
            "Could this be part of moving forward?",
            "What did I learn from it?",
            "What will I do differently next time?",
        ];

        private static readonly string[] dailyTitleKeys =
        [
            "Life",
            "Study",
            "Three positives",
            "Micro failure",
            "Energy / distraction / fatigue",
            //1.6 只有「原因」那一句散文，芯片（八情绪本身）在受管区块里——两者共用一个标题
            "How was today",
        ];

        private static readonly string[] dailyDescriptionKeys =
        [
            LifeDescription,
            StudyDescription,
            ThreePositivesDescription,
            MicroFailureDescription,
            EnergyDescription,
        ];

        // ——— 周课 2.1 / 2.4 / 2.5（其余几节是结构化数据，标题直接写在页面上）———

        public const string SettlementReflectionDescription = "One sentence is enough. \"Nothing comes to mind\" is a valid answer.";

        public const string FourExitsDescription = "Four questions to answer, one by one. They are the exits the whole system drains into.";

        public const string NewThingDescription = "What is one new thing you did this week? Novel experiences land better when shared, so bring someone along if you can.";

        private static readonly string[] weeklyTitleKeys =
        [
            "Settle this week's three goals",
            "Check the four record tables",
            "One new thing this week",
        ];

        private static readonly string[] weeklyDescriptionKeys =
        [
            SettlementReflectionDescription,
            FourExitsDescription,
            NewThingDescription,
        ];

        /// <summary>S/M/A/R/T 五问。定目标（<c>WeeklyGoalEditDialog</c>）与未达成归因（周课 2.1）共用同一份——
        /// 两处各存一份的话，改了问题文字而漏改另一处，用户会看到同一个字母配两个说法。</summary>
        private static readonly string[] weeklySmartKeys =
        [
            "What exactly is the goal?",
            "How will you know you did it?",
            "Can you realistically do it?",
            "Is it worth doing? Does it line up with the bigger direction?",
            "When will it be done?",
        ];

        /// <summary>S/M/A/R/T 五个字母。</summary>
        public static readonly string[] WeeklySmartLetters = ["S", "M", "A", "R", "T"];

        public static string[] WeeklySmartKeys(II18nService i18n)
            => [.. weeklySmartKeys.Select(it => i18n.T(it))];

        /// <summary>2.4 四个记录表检查的四个问题，顺序与文档一致。</summary>
        private static readonly string[] exitQuestionKeys =
        [
            "State record: did you log any this week? Do the triggers repeat an earlier one?",
            "Four lists: what is new this week that should be archived? Say which list, and why that one.",
            "Three virtue tables: anything for the achievement list? Did the gratitude list actually land this week, or was it skimmed?",
            "Affirmation library: how many times did you actually use the phrases the monthly review produced?",
        ];

        // ——— 月课 3.1 ~ 3.4（四步全是结构化数据，标题与说明写在这一处）———

        public const string ThreeColumnReviewDescription = "List up to six limiting beliefs you hold about yourself. \"I am not...\" or \"I will not...\" is a good way to start. Then take them one at a time: what evidence supports it, what evidence contradicts it, and how much does it actually help your wellbeing?";

        public const string CounterStatementsDescription = "Turn the counter-evidence from 3.1 into short phrases and put them in your affirmation library. Three or four is enough - easy to remember and easy to keep up.";

        public const string SelfCompensationDescription = "Accepting this kind of deep criticism is hard, so this step is not optional: give yourself a reward, make time for something you love that is only for you, and list the friends and family who make you feel good and plan to see them soon. This is the one step in the whole system that digs into sore spots - skip the care and you will not want to do the monthly review again.";

        public const string EnemyTableDescription = "Three rows, three columns. First column: a goal you have planned for a long time and never reached. Second column: the behaviour that honestly blocks it - write behaviour, not thoughts. Third column: the deeper belief driving that behaviour. Then give every goal one concrete action you can finish this week.";

        private static readonly string[] monthlyTitleKeys =
        [
            "Three-column review",
            "Counter-statements",
            "Self-compensation",
            "Enemy table",
        ];

        private static readonly string[] monthlyDescriptionKeys =
        [
            ThreeColumnReviewDescription,
            CounterStatementsDescription,
            SelfCompensationDescription,
            EnemyTableDescription,
        ];

        /// <summary>1.1 ~ 1.5 的标题名。</summary>
        public static string[] DailyTitles(II18nService i18n)
            => [.. dailyTitleKeys.Select(it => i18n.T(it))];

        /// <summary>1.1 ~ 1.5 的默认说明，用来给日课模板打底。</summary>
        public static string[] DefaultDailyDescriptions(II18nService i18n)
            => [.. dailyDescriptionKeys.Select(it => i18n.T(it))];

        /// <summary>1.4 四问的题干。写进正文时会回显，导出成 Markdown 也读得懂。</summary>
        public static string[] MicroFailureQuestions(II18nService i18n)
            => [.. microFailureQuestionKeys.Select(it => i18n.T(it))];

        /// <summary>2.4 ~ 2.6 的标题名。</summary>
        public static string[] WeeklyTitles(II18nService i18n)
            => [.. weeklyTitleKeys.Select(it => i18n.T(it))];

        /// <summary>2.4 ~ 2.6 的默认说明。</summary>
        public static string[] WeeklyDescriptions(II18nService i18n)
            => [.. weeklyDescriptionKeys.Select(it => i18n.T(it))];

        /// <summary>2.4 四个出口问题的题干。写进正文时会回显。</summary>
        public static string[] ExitQuestions(II18nService i18n)
            => [.. exitQuestionKeys.Select(it => i18n.T(it))];

        /// <summary>3.1 ~ 3.4 的标题名。</summary>
        public static string[] MonthlyTitles(II18nService i18n)
            => [.. monthlyTitleKeys.Select(it => i18n.T(it))];

        /// <summary>3.1 ~ 3.4 的默认说明。</summary>
        public static string[] MonthlyDescriptions(II18nService i18n)
            => [.. monthlyDescriptionKeys.Select(it => i18n.T(it))];

        /// <summary>
        /// 月课摘要里的那些栏目标签（三列审视的三列、敌人表的四列）。
        /// 单独给一份，是因为摘要要渲染成 Markdown，标签得跟着界面语言走。
        /// </summary>
        public static MonthlyReviewLabels MonthlyLabels(II18nService i18n)
        {
            //四个步骤名直接用上面那组标题，别再抄一遍字面量——两处迟早会有一个改了另一个没改
            var titles = MonthlyTitles(i18n);

            return new MonthlyReviewLabels(
                titles[0],
                titles[1],
                titles[2],
                titles[3],
                i18n.T("Supporting evidence"),
                i18n.T("Counter evidence"),
                i18n.T("How much does this belief actually help your wellbeing?"),
                i18n.T("Goal"),
                i18n.T("Blocking behaviour"),
                i18n.T("Deep belief"),
                i18n.T("One concrete action this week"));
        }

        // ——— 节拍设置（需求 14，设置页的「复盘节拍」组）———

        public const string AnchorDescription =
            "The weekly review is anchored to this weekday. The book puts it on Friday, so the week's material is still fresh and the weekend is still ahead of you.";

        public const string WeeklyGraceDescription =
            "How many days after the anchor the weekly review is still merely pending instead of overdue. The last week of the month is the usual reason to widen this.";

        public const string MonthlyGraceDescription =
            "How many days into the next month the monthly review is still merely pending. The end of a month is a busy time; a few days of slack keeps it from being marked overdue on day one.";

        // ——— 枚举 → 文案 ———
        // 这三个映射同时被「页面显示」和「正文里的生成式摘要」用到。收在这里，
        // 页面上写着「环境原因」而导出的正文里写成别的词，是最难发现的那类不一致。

        /// <summary>
        /// 目标领域。<paramref name="domain"/> 可以为 null —— 文档原话是
        /// 「三个宏观方向各挂到一个领域上，挂不上也没关系」。
        /// </summary>
        public static string DomainText(II18nService i18n, GoalDomain? domain)
        {
            if (domain is null)
            {
                return i18n.T("No domain");
            }

            return i18n.T(domain switch
            {
                GoalDomain.Emotion => "Emotion",
                GoalDomain.Work => "Work",
                _ => "Personal development",
            });
        }

        public static string StatusText(II18nService i18n, GoalStatus status)
        {
            return i18n.T(status switch
            {
                GoalStatus.Done => "Done",
                GoalStatus.Dropped => "Not done",
                GoalStatus.InProgress => "Partly done",
                _ => "Pending",
            });
        }

        public static string VerdictText(II18nService i18n, BlockVerdict verdict)
        {
            return i18n.T(verdict switch
            {
                BlockVerdict.StumblingBlock => "Stumbling block",
                BlockVerdict.Environment => "Environment reason",
                BlockVerdict.Rejected => "Rejected this week",
                _ => "Undecided",
            });
        }

        /// <summary>归档区四张 List 的名字。归档区页面与周课 2.4 的归档面板共用这一份。</summary>
        public static string ArchiveKindText(II18nService i18n, ArchiveKind kind)
        {
            return i18n.T(kind switch
            {
                ArchiveKind.FuturePlan => "New plans for the future",
                ArchiveKind.DailyThought => "Thoughts about daily life",
                ArchiveKind.MicroFailure => "Micro failures",
                _ => "New understanding of myself",
            });
        }
    }
}
