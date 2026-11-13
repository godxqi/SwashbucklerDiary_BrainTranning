using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>周课 2.1 列表里的一条：失败本身 + 它来自哪一天（日期取自 <c>SourceDiaryId</c> 指向的那篇日记）。</summary>
    public sealed record StumblingBlockView(StumblingBlockModel Block, DateTime? Date);

    /// <summary>
    /// 周课 2.1「数频率」的数据服务。
    /// <para>
    /// 上游是日课 1.4：保存时把每条微观失败同步成一行；
    /// 下游是周课：把这一周的失败列出来，由<b>人</b>归类、由<b>人</b>判性——机器只数次数。
    /// </para>
    /// </summary>
    public interface IStumblingBlockService
    {
        /// <summary>
        /// 把某天日课里 1.4 的每条失败增量同步成一行。
        /// <paramref name="microFailures"/> 的<b>下标</b>就是 <see cref="StumblingBlockModel.OrderIndex"/>。
        /// 已判过（<see cref="StumblingBlockModel.Verdict"/> 非未定 / 已归类）的行只更新描述，
        /// 判定与归类原样保留——那是周课的人工判断，重存一次日课不该把它冲掉。
        /// <para>
        /// 认领顺序是「<b>先按描述文本、认不到再按序号</b>」：回改过去某天的日课时，
        /// 中间插一条 / 删一条 / 换顺序都不会让周课那边的判定静默地指到别人身上。
        /// </para>
        /// </summary>
        Task SyncFromDailyAsync(Guid diaryId, DateTime date, IReadOnlyList<string[]> microFailures);

        /// <summary>某一周的全部失败，按来源日期、再按当天序号排序。</summary>
        Task<List<StumblingBlockView>> GetByWeekAsync(int year, int weekOfYear);

        /// <summary>
        /// 删掉某篇日课派生出来的<b>全部</b>失败行（含已经判过的）。
        /// <para>
        /// 只在日课页的「删除这次日课」时用——那是用户显式点的删除，跟「改日课时顺手清掉一条」
        /// 不是一回事：后者的规矩是判过的行一律留着。所以这条路径要显式调用，
        /// 不能悄悄塞进 <see cref="SyncFromDailyAsync"/>。
        /// </para>
        /// <para>
        /// 注意：从这些行流进归档区 7.4 的条目<b>不会</b>跟着删。归档条目有自己的去向（7.4 还会回流），
        /// 而且用户可能已经在归档区里改写过它，按来源反查并不可靠。
        /// </para>
        /// </summary>
        Task DeleteBySourceDiaryAsync(Guid diaryId);

        /// <summary>
        /// 某一个自然月的全部失败。月课 3.1 的素材之一是「这一月反复出现的绊脚石」。
        /// <para>
        /// 失败行没有自己的日期（日期在 <see cref="StumblingBlockModel.SourceDiaryId"/> 指向的日记上），
        /// 所以只能按它落在哪些 <b>ISO 周</b>来圈——一个自然月横跨 4~6 个连续的 ISO 周，
        /// 跨年时分成两段。这样不用给表加日期列，也不用去 join 整月的日记。
        /// </para>
        /// </summary>
        Task<List<StumblingBlockView>> GetByMonthAsync(int year, int month);

        /// <summary>
        /// 历史上归过的类别（去重、非空），用来做周课归类时的建议芯片。
        /// 类别是边归边长出来的，所以这里没有种子文件。
        /// </summary>
        Task<List<string>> GetKnownCategoriesAsync();

        /// <summary>
        /// 周课 2.1 的人工判定：写 <see cref="StumblingBlockModel.Verdict"/> 与类别，并按结果落归档区
        /// ——「原因是环境 / 别人不配合」与「本周落选」正是文档 7.4 的两个入口。
        /// </summary>
        Task<bool> JudgeAsync(StumblingBlockModel block, BlockVerdict verdict, string? category);
    }
}
