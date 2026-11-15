using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncReviewHandler : ISyncReviewHandler
    {
        private const string ReviewPath = "review.json";

        private const string LocationsPath = "locations.json";

        private readonly ISyncStorage _storage;

        private readonly ISyncObjectCodec _codec;

        private readonly IReviewExportService _reviewExportService;

        private readonly ILocationService _locationService;

        public SyncReviewHandler(
            ISyncStorage storage,
            ISyncObjectCodec codec,
            IReviewExportService reviewExportService,
            ILocationService locationService)
        {
            _storage = storage;
            _codec = codec;
            _reviewExportService = reviewExportService;
            _locationService = locationService;
        }

        public async Task MergeReviewAsync(
            SyncLocalSnapshot local,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct)
        {
            var localModel = await _reviewExportService.ExportAsync().ConfigureAwait(false);
            var (remoteModel, remoteBytes) = await ReadReviewAsync(ct).ConfigureAwait(false);

            var graves = MergeGraves(local, remote);
            var merged = SyncReviewMerger.Merge(localModel, remoteModel, graves);
            byte[] mergedBytes = _codec.SerializeReview(merged);

            bool upload = remoteBytes is null || !remoteBytes.AsSpan().SequenceEqual(mergedBytes);

            using (SyncContext.BeginSuppressUpdateTimeRefresh())
            using (SyncContext.BeginSuppressGraveRecording())
            {
                await _reviewExportService.ApplyMergedAsync(localModel, merged).ConfigureAwait(false);
            }

            // 两端都没有复盘数据时不要凭空造一个空 review.json。
            if (merged.IsEmpty && remoteBytes is null)
            {
                remote.Review.Remove(SyncReviewTables.AllKey);
                return;
            }

            if (upload)
            {
                using var stream = new MemoryStream(mergedBytes, writable: false);
                await _storage.PutAsync(ReviewPath, stream, null, ct).ConfigureAwait(false);
                result.Uploaded++;
            }
            else
            {
                result.Downloaded++;
            }

            remote.Review[SyncReviewTables.AllKey] = new SyncObjectMeta
            {
                UpdateTime = SyncReviewMerger.MaxUpdateTime(merged),
                Hash = _codec.Hash(mergedBytes),
            };
        }

        public async Task MergeLocationsAsync(
            SyncLocalSnapshot local,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct)
        {
            var localRows = await _locationService.QueryAsync().ConfigureAwait(false);
            var (remoteRows, remoteBytes) = await ReadLocationsAsync(ct).ConfigureAwait(false);

            var graves = MergeGraves(local, remote);
            var merged = SyncReviewMerger.MergeRows(
                localRows,
                remoteRows,
                "location",
                graves,
                SyncEntityKeys.LocationPrefix);

            byte[] mergedBytes = _codec.SerializeLocations(merged);
            bool upload = remoteBytes is null || !remoteBytes.AsSpan().SequenceEqual(mergedBytes);

            using (SyncContext.BeginSuppressUpdateTimeRefresh())
            using (SyncContext.BeginSuppressGraveRecording())
            {
                // 删除差集 + 逐行 upsert。不要「清空再重插」：中间失败会整表清空。
                var mergedIds = merged.Select(it => it.Id).ToHashSet();
                var toDelete = localRows.Where(it => !mergedIds.Contains(it.Id)).ToList();
                if (toDelete.Count > 0)
                {
                    await _locationService.DeleteAsync(toDelete).ConfigureAwait(false);
                }

                var localIds = localRows.Select(it => it.Id).ToHashSet();
                foreach (var row in merged)
                {
                    if (localIds.Contains(row.Id))
                    {
                        await _locationService.UpdateAsync(row).ConfigureAwait(false);
                    }
                    else
                    {
                        await _locationService.AddAsync(row).ConfigureAwait(false);
                    }
                }
            }

            if (upload)
            {
                using var stream = new MemoryStream(mergedBytes, writable: false);
                await _storage.PutAsync(LocationsPath, stream, null, ct).ConfigureAwait(false);
                result.Uploaded++;
            }
            else
            {
                result.Downloaded++;
            }

            remote.Locations = new SyncObjectMeta
            {
                UpdateTime = SyncReviewMerger.MaxOf(merged),
                Hash = _codec.Hash(mergedBytes),
            };
        }

        private async Task<(ReviewExportModel Model, byte[]? Bytes)> ReadReviewAsync(CancellationToken ct)
        {
            var file = await _storage.GetAsync(ReviewPath, ct).ConfigureAwait(false);
            if (file is null)
            {
                return (new ReviewExportModel(), null);
            }

            byte[] bytes = await ReadAllAsync(file, ct).ConfigureAwait(false);
            return (_codec.DeserializeReview(bytes) ?? new ReviewExportModel(), bytes);
        }

        private async Task<(List<LocationModel> Rows, byte[]? Bytes)> ReadLocationsAsync(CancellationToken ct)
        {
            var file = await _storage.GetAsync(LocationsPath, ct).ConfigureAwait(false);
            if (file is null)
            {
                return ([], null);
            }

            byte[] bytes = await ReadAllAsync(file, ct).ConfigureAwait(false);
            return (_codec.DeserializeLocations(bytes) ?? [], bytes);
        }

        private static Dictionary<string, long> MergeGraves(SyncLocalSnapshot local, SyncManifest remote)
        {
            var merged = new Dictionary<string, long>(local.Graves, StringComparer.Ordinal);
            foreach (var (key, deletedAt) in remote.Graves)
            {
                merged[key] = Math.Max(merged.GetValueOrDefault(key), deletedAt);
            }

            return merged;
        }

        private static async Task<byte[]> ReadAllAsync(SyncFile file, CancellationToken ct)
        {
            await using (file.Content)
            {
                using var buffer = new MemoryStream();
                await file.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
                return buffer.ToArray();
            }
        }
    }
}
