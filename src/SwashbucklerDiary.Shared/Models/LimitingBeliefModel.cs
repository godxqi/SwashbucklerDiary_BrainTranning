namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 月课 3.1 三列审视的一行。最多六条。
    /// 素材来源是状态管理表攒下的触因，加上这一月反复出现的绊脚石。
    /// </summary>
    public class LimitingBeliefModel : BaseModel
    {
        public Guid MonthlyReviewId { get; set; }

        /// <summary>第一列：限制性观念。卡壳时用「我并不是……」「我不会……」开头。</summary>
        public string? Assertion { get; set; }

        /// <summary>第二列：我相信这句话的证据是什么。</summary>
        public string? SupportingEvidence { get; set; }

        /// <summary>第三列：反面证据。这是 3.2 提炼自我肯定的原料。</summary>
        public string? CounterEvidence { get; set; }

        /// <summary>第四步：评估效用——这个观念对我的幸福有多大帮助。</summary>
        public string? Utility { get; set; }
    }
}
