using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 四个 List · 归档区（方法文档 7）。
    /// <para>
    /// 归档区不是待办：只有周课 2.5 的出口问题（以及它上游的 2.1 / 2.4）才往这里写。
    /// </para>
    /// </summary>
    public interface IArchiveService
    {
        Task<List<ArchiveItemModel>> QueryAsync(ArchiveKind? kind = null);

        /// <summary>
        /// 记一条归档。同一个 List 里<b>内容相同</b>的条目不是新增，而是「又出现了一次」——
        /// 文档 7.4 的出口就是靠这个计数（累计 3 次说明它不是偶发），所以去重与计数在这里收口。
        /// </summary>
        Task<bool> ArchiveAsync(ArchiveKind kind,
            ArchiveSource source,
            string? content,
            Guid? sourceWeeklyReviewId = null);

        /// <summary>
        /// 7.4 里攒够次数、还没回流的条目。复盘主页的「该回流了」提醒与归档区页顶部都用它。
        /// </summary>
        Task<List<ArchiveItemModel>> GetFeedbackCandidatesAsync(int minCount = 3);

        /// <summary>
        /// 7.4 里累计到 <paramref name="minCount"/> 次的条目内容。
        /// <para>
        /// 文档 7.4 的出口写的是「累计 3 次 → 说明它不是偶发，<b>回到 2.1 重新进入候选池</b>」。
        /// 所以周课 2.2 的候选池除了「同类 ≥2 次」「人工判成绊脚石」之外，还要认这一条——
        /// 否则回流回来的那条在 2.1 里看得见、却永远进不了池。
        /// 回流时归档内容被原样写成了失败行的 Description，所以按描述能精确对上。
        /// </para>
        /// </summary>
        Task<List<string>> GetRecurringContentsAsync(int minCount = 3);

        /// <summary>
        /// 7.4 的出口（文档原文）：同一个条目累计 3 次，说明它不是偶发。
        /// 做两件事：标记已回流到 <paramref name="date"/> 所在那一周，并在那一周
        /// <b>新建一条判为「未定」的微观失败</b>——它就重新出现在周课 2.1 里等人归类。
        /// <para>
        /// 建行这一步<b>直接写 <see cref="StumblingBlockModel"/> 的仓储</b>，不注入
        /// <see cref="IStumblingBlockService"/>：那边已经依赖本服务（2.1 判定要落归档），
        /// 反向注入会形成循环依赖。回流行没有 <c>SourceDiaryId</c>，所以
        /// <c>SyncFromDailyAsync</c> 不会扫到它——这正是想要的，它不属于任何一天的日课。
        /// </para>
        /// </summary>
        Task<bool> FeedBackAsync(Guid id, DateTime date);

        /// <summary>手动补充的条目要能删。7.4 里判过的东西也可以在这里清掉。</summary>
        Task<bool> DeleteAsync(Guid id);
    }
}
