using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;
using System.Linq.Expressions;

namespace SwashbucklerDiary.Rcl.Services
{
    public class LocationService : BaseDataService<LocationModel>, ILocationService
    {
        private readonly ILocationRepository _iLocationRepository;

        private readonly ISyncGraveService _graveService;

        public LocationService(ILocationRepository iLocationRepository, ISyncGraveService graveService)
        {
            base._iBaseRepository = iLocationRepository;
            _iLocationRepository = iLocationRepository;
            _graveService = graveService;
        }

        // 删除顺序同 DiaryService：先删成功再写墓碑，避免删除失败却把删除传播到另一端。

        public override async Task<bool> DeleteAsync()
        {
            var items = await QueryAsync().ConfigureAwait(false);
            bool result = await base.DeleteAsync().ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(items).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(LocationModel entity)
        {
            bool result = await base.DeleteAsync(entity).ConfigureAwait(false);
            if (result)
            {
                await RecordGraveAsync(entity.Id).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(Guid id)
        {
            bool result = await base.DeleteAsync(id).ConfigureAwait(false);
            if (result)
            {
                await RecordGraveAsync(id).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(List<LocationModel> entities)
        {
            bool result = await base.DeleteAsync(entities).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(entities).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(Expression<Func<LocationModel, bool>> expression)
        {
            var items = await QueryAsync(expression).ConfigureAwait(false);
            bool result = await base.DeleteAsync(expression).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(items).ConfigureAwait(false);
            }

            return result;
        }

        private async Task RecordGravesAsync(IEnumerable<LocationModel> items)
        {
            if (SyncContext.SuppressGraveRecording)
            {
                return;
            }

            foreach (var item in items)
            {
                await RecordGraveAsync(item.Id).ConfigureAwait(false);
            }
        }

        private async Task RecordGraveAsync(Guid id)
        {
            if (SyncContext.SuppressGraveRecording || id == Guid.Empty)
            {
                return;
            }

            // LocationPrefix 是 "location:"，AddAsync 拼成 "location:{id}"，
            // 与 SyncReviewMerger 里地点行墓碑的键格式一致。
            await _graveService.AddAsync(SyncEntityKeys.LocationKey, id).ConfigureAwait(false);
        }
    }
}
