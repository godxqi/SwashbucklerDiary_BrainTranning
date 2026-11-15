using SqlSugar;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;
using System.Linq.Expressions;

namespace SwashbucklerDiary.Rcl.Repository
{
    public class ReviewRepository<T> : BaseRepository<T>, IReviewRepository<T> where T : class, new()
    {
        private readonly ISyncGraveService _graveService;

        public ReviewRepository(
            ISqlSugarClient context,
            ISettingService settingService,
            ISyncGraveService graveService) : base(context, settingService)
        {
            _graveService = graveService;
        }

        public ISugarQueryable<T> Queryable()
        {
            return Context.Queryable<T>();
        }

        public async Task<bool> UseTranAsync(Func<ISqlSugarClient, Task> action)
        {
            var context = Context;
            var result = await context.Ado.UseTranAsync(() => action(context)).ConfigureAwait(false);
            return result.IsSuccess;
        }

        // 复盘行删除要能跨设备传播。15 张表同构，统一在这里拦截，避免逐个服务复制。
        // 顺序同 DiaryService：先删成功再写墓碑。

        public override async Task<bool> DeleteAsync()
        {
            // 按查出来的列表删，而不是无条件 DeleteAll：查询与删除之间新增的行
            // 不会被误删且漏墓碑。
            var items = await Queryable().ToListAsync().ConfigureAwait(false);
            if (items.Count == 0)
            {
                return false;
            }

            return await DeleteAsync(items).ConfigureAwait(false);
        }

        public override async Task<bool> DeleteAsync(T deleteObj)
        {
            bool result = await base.DeleteAsync(deleteObj).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync([deleteObj]).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteByIdAsync(object id)
        {
            bool result = await base.DeleteByIdAsync(id).ConfigureAwait(false);
            if (result && id is Guid guid)
            {
                await RecordGraveAsync(guid).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(List<T> deleteObjs)
        {
            bool result = await base.DeleteAsync(deleteObjs).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(deleteObjs).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(Expression<Func<T, bool>> whereExpression)
        {
            var items = await Queryable().Where(whereExpression).ToListAsync().ConfigureAwait(false);
            bool result = await base.DeleteAsync(whereExpression).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(items).ConfigureAwait(false);
            }

            return result;
        }

        private async Task RecordGravesAsync(IEnumerable<T> items)
        {
            if (SyncContext.SuppressGraveRecording)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item is BaseModel model)
                {
                    await RecordGraveAsync(model.Id).ConfigureAwait(false);
                }
            }
        }

        private async Task RecordGraveAsync(Guid id)
        {
            if (SyncContext.SuppressGraveRecording || id == Guid.Empty)
            {
                return;
            }

            string? tableKey = SyncReviewTables.GetKey(typeof(T));
            if (tableKey is null)
            {
                return;
            }

            // 生成 "review:{table}:{rowId}"，与 SyncReviewMerger 的行墓碑键一致。
            await _graveService.AddAsync(SyncEntityKeys.ReviewPrefix + tableKey, id).ConfigureAwait(false);
        }
    }
}
