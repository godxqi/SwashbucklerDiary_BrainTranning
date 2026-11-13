namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 周课（方法文档 2）的载体。2.5 四处出口检查与 2.6 本周一件新事物是散文，写在日记 Content 里。
    /// </summary>
    public class WeeklyReviewModel : BaseModel
    {
        public int Year { get; set; }

        /// <summary>ISO 周次。与 <see cref="Year"/> 组成唯一键。</summary>
        public int WeekOfYear { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public Guid? DiaryId { get; set; }

        /// <summary>2.1 数频率是否已确认。</summary>
        public bool FrequencyConfirmed { get; set; }

        /// <summary>2.3 三个目标是否已确认。</summary>
        public bool GoalsConfirmed { get; set; }

        public bool IsFinalized { get; set; }

        public DateTime? FinalizedTime { get; set; }

        /// <summary>
        /// 这一周「跳过」——补足时用来声明「这段我真的没写，不算欠」。
        /// <para>
        /// 它只影响统计口径（完成度的分母扣掉这一周），不挡写：一写东西就自动取消
        /// （见 <c>WeeklyReviewService.SaveAsync</c>）。级联到日课：被跳过的周里那 7 天也不计入日课分母。
        /// </para>
        /// <para>
        /// <b>必须可空</b>：SqlSugar 的 <c>InitTables</c> 给已有表补列时补的是<b>可空</b>列
        /// （实测 DDL：<c>`IsSkipped` bit</c>），老行读出来是 NULL。声明成非空 <c>bool</c> 的话，
        /// 反序列化直接抛 <c>JsonSerializationException: Error converting value {null} to type 'System.Boolean'</c>。
        /// 判定一律写 <c>== true</c>，别把它当非空用。
        /// </para>
        /// </summary>
        public bool? IsSkipped { get; set; }
    }
}
