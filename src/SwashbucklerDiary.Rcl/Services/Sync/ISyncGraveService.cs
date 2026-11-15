using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 墓碑读写。删除业务代码只负责调用 <see cref="AddAsync(string, Guid)"/>，
    /// 拼键和去重都在这里做。
    /// </summary>
    public interface ISyncGraveService
    {
        /// <summary>记录一次按 Guid 的删除。</summary>
        Task AddAsync(string entityType, Guid entityId);

        /// <summary>记录一次按内容键的删除（附件等非 Guid 实体）。</summary>
        Task AddAsyncByKeyAsync(string entityType, string key);

        /// <summary>查询墓碑；<paramref name="since"/> 为空时返回全部。</summary>
        Task<List<SyncGraveModel>> QueryAsync(DateTime? since = null);

        /// <summary>删除墓碑（远端对象比墓碑新时，视为"复活"，撤销墓碑）。</summary>
        Task RemoveAsync(IEnumerable<Guid> ids);

        /// <summary>按统一键撤销墓碑。</summary>
        Task RemoveByKeyAsync(string entityKey);

        /// <summary>清理早于阈值的墓碑，防止本地表和 manifest 无限增长。</summary>
        Task ClearOlderThanAsync(DateTime threshold);
    }
}
