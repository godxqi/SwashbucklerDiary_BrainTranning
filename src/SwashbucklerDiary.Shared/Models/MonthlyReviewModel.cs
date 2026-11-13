namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 月课（方法文档 3）的载体。四步是带闸门的向导，每一步有产出才放行下一步。
    /// </summary>
    public class MonthlyReviewModel : BaseModel
    {
        public int Year { get; set; }

        public int Month { get; set; }

        public Guid? DiaryId { get; set; }

        /// <summary>3.1 三列审视：闸门 = LimitingBeliefModel 至少 1 条。</summary>
        public bool Step31Done { get; set; }

        /// <summary>3.2 提炼短语进自我肯定库：闸门 = 本次新增 SelfAffirmationModel 至少 1 条。</summary>
        public bool Step32Done { get; set; }

        /// <summary>3.3 自我补偿：闸门 = <see cref="Step33Note"/> 非空。这一步不许跳过。</summary>
        public bool Step33Done { get; set; }

        /// <summary>3.4 敌人表：闸门 = EnemyModel 至少 1 条。</summary>
        public bool Step34Done { get; set; }

        /// <summary>3.3 的产出（给自己的奖励 / 只为自己做的事 / 要见的人）。</summary>
        public string? Step33Note { get; set; }

        public bool IsFinalized { get; set; }

        public DateTime? FinalizedTime { get; set; }

        /// <summary>
        /// 这一月「跳过」——补足时用来声明「这整段我真的没写，不算欠」。
        /// <para>
        /// 只影响统计口径，不挡写：一写东西就自动取消（见 <c>MonthlyReviewService.SaveAsync</c>）。
        /// 级联到周课与日课：被跳过的月里那几周、那几十天都不计入各自分母。
        /// </para>
        /// <para>
        /// <b>必须可空</b>，理由同 <see cref="WeeklyReviewModel.IsSkipped"/>：补列补出来的是可空列，
        /// 老行的值是 NULL，声明成非空 <c>bool</c> 会读一次炸一次。判定一律写 <c>== true</c>。
        /// </para>
        /// </summary>
        public bool? IsSkipped { get; set; }
    }
}
