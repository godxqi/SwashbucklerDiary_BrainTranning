using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>一个触因 + 它出现了几次。</summary>
    public sealed record TriggerCount(TagModel Tag, int Count);

    /// <summary>
    /// 触因（需求 12 / 方法文档 5.2）。它复用整条标签管线，只是把 <see cref="TagModel.Kind"/>
    /// 标成 <see cref="TagKind.Trigger"/>。
    /// <para>
    /// 次数不是「打了这个标签的日记有几篇」，而是<b>状态管理表里出现过几次</b>——
    /// 文档 5.2 要的是「攒 3 个以上同类触因」，单位是一次次发作，不是日记数。
    /// </para>
    /// </summary>
    public interface ITriggerService
    {
        /// <summary>全部触因 + 出现次数，次数多的排前面。</summary>
        Task<List<TriggerCount>> GetWithCountsAsync();

        /// <summary>攒够 <paramref name="minCount"/> 次、该交给月课 3.1 第一列的触因（文档 5.2）。</summary>
        Task<List<TriggerCount>> GetReadyForMonthlyAsync(int minCount = 3);

        Task<bool> AddAsync(string name);

        Task<bool> DeleteAsync(Guid id);

        /// <summary>
        /// 快速记一次：插一条只填了时间与触因的状态记录。
        /// 阶段 4 的完整表单（场景 / 四种感受 / 调用哪一条）复用同一张表，届时这里记的会成为一条待补全的记录。
        /// </summary>
        Task<bool> RecordOccurrenceAsync(Guid tagId, DateTime? occurredTime = null);
    }
}
