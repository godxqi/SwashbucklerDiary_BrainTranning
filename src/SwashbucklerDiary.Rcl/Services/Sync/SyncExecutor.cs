using Microsoft.Extensions.Logging;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 按实体类型分发同步计划。具体执行在
    /// <see cref="ISyncDiaryHandler"/> / <see cref="ISyncAssetHandler"/> / <see cref="ISyncReviewHandler"/>。
    /// </summary>
    public class SyncExecutor : ISyncExecutor
    {
        private readonly ISyncDiaryHandler _diaryHandler;

        private readonly ISyncAssetHandler _assetHandler;

        private readonly ISyncReviewHandler _reviewHandler;

        private readonly ILogger<SyncExecutor> _logger;

        public SyncExecutor(
            ISyncDiaryHandler diaryHandler,
            ISyncAssetHandler assetHandler,
            ISyncReviewHandler reviewHandler,
            ILogger<SyncExecutor> logger)
        {
            _diaryHandler = diaryHandler;
            _assetHandler = assetHandler;
            _reviewHandler = reviewHandler;
            _logger = logger;
        }

        public async Task<SyncExecuteResult> ExecuteAsync(
            SyncPlan plan,
            SyncManifest remote,
            SyncLocalSnapshot local,
            SyncState state,
            CancellationToken ct = default)
        {
            var result = new SyncExecuteResult();

            // 附件单独走并发，先处理完再进串行的对象循环。
            await _assetHandler.ExecuteAsync(
                plan.Changes.Where(it => it.EntityType == SyncEntityType.Asset).ToList(),
                remote,
                result,
                ct).ConfigureAwait(false);

            // 复盘/地点是整包对象，plan 里可能因为墓碑出现多条同类型 change；
            // 每轮只合并一次，否则重复跑同样的大合并。
            bool reviewHandled = false;
            bool locationHandled = false;

            foreach (var change in plan.Changes)
            {
                if (change.EntityType == SyncEntityType.Asset)
                {
                    continue;
                }

                ct.ThrowIfCancellationRequested();

                try
                {
                    switch (change.EntityType)
                    {
                        case SyncEntityType.Diary:
                            await _diaryHandler
                                .ExecuteAsync(change, remote, state, result, ct)
                                .ConfigureAwait(false);
                            break;

                        case SyncEntityType.Review:
                            if (!reviewHandled)
                            {
                                reviewHandled = true;
                                await _reviewHandler
                                    .MergeReviewAsync(local, remote, result, ct)
                                    .ConfigureAwait(false);
                            }

                            break;

                        case SyncEntityType.Location:
                            if (!locationHandled)
                            {
                                locationHandled = true;
                                await _reviewHandler
                                    .MergeLocationsAsync(local, remote, result, ct)
                                    .ConfigureAwait(false);
                            }

                            break;

                        default:
                            result.Skipped++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // 单个对象失败不能拖垮整轮同步：已成功的部分照常提交，失败的下轮重试。
                    result.Errors.Add($"{change.Key}: {ex.Message}");
                    _logger.LogError(ex, "Sync change failed: {Key} {Action}", change.Key, change.Action);
                }
            }

            return result;
        }
    }
}
