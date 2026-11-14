using SqlSugar;

namespace SwashbucklerDiary.Rcl.Repository
{
    /// <summary>
    /// 复盘系统那 12 张表彼此同构（都是 BaseModel + 自己的几个字段），
    /// 所以共用一个泛型仓储，而不是写 12 份逐字复制的样板。
    /// </summary>
    public interface IReviewRepository<T> : IBaseRepository<T> where T : class, new()
    {
        /// <summary>
        /// 拿到一个已经按隐私模式切好连接的查询器。
        /// 聚合统计走这里的 SQL，不要在 C# 里遍历全表。
        /// </summary>
        ISugarQueryable<T> Queryable();

        /// <summary>
        /// 在同一连接上跑一组写操作；失败一起回滚。
        /// 隐私模式切库由仓储自己处理，调用方不要另开连接。
        /// </summary>
        Task<bool> UseTranAsync(Func<ISqlSugarClient, Task> action);
    }
}
