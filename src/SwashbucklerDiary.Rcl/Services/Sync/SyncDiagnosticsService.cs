using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncDiagnosticsService : ISyncDiagnosticsService
    {
        private readonly ISyncGraveRepository _repository;

        public SyncDiagnosticsService(ISyncGraveRepository repository)
        {
            _repository = repository;
        }

        public async Task<SyncProbeStep> ProbeUpdateTimeAopAsync(CancellationToken ct = default)
        {
            const string name = "UpdateTime 兜底（AOP）";
            const string probeType = "sync-probe";

            var probeId = Guid.NewGuid();
            var model = new SyncGraveModel
            {
                // 项目里 Guid 主键全部由业务代码生成，SqlSugar 不会代劳。
                Id = Guid.NewGuid(),
                EntityType = probeType,
                EntityId = probeId,
                EntityKey = $"{probeType}:{probeId:N}",
                DeletedTime = DateTime.Now,
            };

            try
            {
                await _repository.InsertAsync(model).ConfigureAwait(false);

                var added = await _repository.GetByIdAsync(model.Id).ConfigureAwait(false);
                if (added is null)
                {
                    return new SyncProbeStep(name, false, "墓碑未写入");
                }

                bool insertOk = added.UpdateTime != default;
                DateTime before = added.UpdateTime;

                await Task.Delay(30, ct).ConfigureAwait(false);

                // 整实体更新：AOP 会把 UpdateTime 刷成当前时间并落库。
                await _repository.UpdateAsync(added).ConfigureAwait(false);

                var touched = await _repository.GetByIdAsync(model.Id).ConfigureAwait(false);
                bool updateOk = touched is not null && touched.UpdateTime > before;

                await _repository.DeleteByIdAsync(model.Id).ConfigureAwait(false);

                return new SyncProbeStep(
                    name,
                    insertOk && updateOk,
                    $"Insert={insertOk}, Update={updateOk}, AOP操作类型={SyncDiagnostics.LastOperationType ?? "<未触发>"}"
                    + $"（{before:O} → {touched?.UpdateTime:O}）");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await TryCleanupAsync(model.Id).ConfigureAwait(false);
                return new SyncProbeStep(name, false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task TryCleanupAsync(Guid id)
        {
            try
            {
                await _repository.DeleteByIdAsync(id).ConfigureAwait(false);
            }
            catch
            {
                // 自检失败后的清理失败没有更好的处理方式；残留一条 sync-probe 墓碑不影响业务。
            }
        }
    }
}
