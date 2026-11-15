using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncGraveService : ISyncGraveService
    {
        private readonly ISyncGraveRepository _repository;

        public SyncGraveService(ISyncGraveRepository repository)
        {
            _repository = repository;
        }

        public Task AddAsync(string entityType, Guid entityId)
            => InternalAddAsync(BuildKey(entityType, entityId), entityType, entityId);

        public Task AddAsyncByKeyAsync(string entityType, string key)
            => InternalAddAsync($"{entityType}:{key}", entityType, Guid.Empty);

        public Task<List<SyncGraveModel>> QueryAsync(DateTime? since = null)
        {
            return since is null
                ? _repository.GetListAsync()
                : _repository.GetListAsync(it => it.DeletedTime > since.Value);
        }

        public Task RemoveAsync(IEnumerable<Guid> ids)
        {
            var idList = ids.Distinct().ToList();
            if (idList.Count == 0)
            {
                return Task.CompletedTask;
            }

            return _repository.DeleteAsync(it => idList.Contains(it.Id));
        }

        public async Task RemoveByKeyAsync(string entityKey)
        {
            var grave = await _repository.GetFirstAsync(it => it.EntityKey == entityKey)
                .ConfigureAwait(false);
            if (grave is null)
            {
                return;
            }

            await _repository.DeleteByIdAsync(grave.Id).ConfigureAwait(false);
        }

        public Task ClearOlderThanAsync(DateTime threshold)
            => _repository.DeleteAsync(it => it.DeletedTime < threshold);

        private async Task InternalAddAsync(string entityKey, string entityType, Guid entityId)
        {
            var existing = await _repository.GetFirstAsync(it => it.EntityKey == entityKey)
                .ConfigureAwait(false);

            if (existing is null)
            {
                await _repository.InsertAsync(new SyncGraveModel
                {
                    // SqlSugar 不会为 Guid 主键自动生成值（项目里所有业务插入都手动赋值），
                    // 漏掉这一行会让第二条墓碑直接撞 Guid.Empty 主键。
                    Id = Guid.NewGuid(),
                    EntityType = entityType,
                    EntityId = entityId,
                    EntityKey = entityKey,
                    DeletedTime = DateTime.Now,
                }).ConfigureAwait(false);
                return;
            }

            // 重复删除同一个对象：只更新时间，不新增行。
            existing.DeletedTime = DateTime.Now;
            await _repository.UpdateAsync(existing, it => new { it.DeletedTime }).ConfigureAwait(false);
        }

        private static string BuildKey(string entityType, Guid entityId)
            => $"{entityType}:{entityId:N}";
    }
}
