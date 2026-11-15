using System.Security.Cryptography;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncSnapshotService : ISyncSnapshotService
    {
        private readonly IDiaryService _diaryService;

        private readonly IMediaResourceManager _mediaResourceManager;

        private readonly ISyncGraveService _graveService;

        private readonly ISyncObjectCodec _codec;

        private readonly IReviewExportService _reviewExportService;

        private readonly ILocationService _locationService;

        public SyncSnapshotService(
            IDiaryService diaryService,
            IMediaResourceManager mediaResourceManager,
            ISyncGraveService graveService,
            ISyncObjectCodec codec,
            IReviewExportService reviewExportService,
            ILocationService locationService)
        {
            _diaryService = diaryService;
            _mediaResourceManager = mediaResourceManager;
            _graveService = graveService;
            _codec = codec;
            _reviewExportService = reviewExportService;
            _locationService = locationService;
        }

        public async Task<SyncLocalSnapshot> CaptureAsync(CancellationToken ct = default)
        {
            var review = await _reviewExportService.ExportAsync().ConfigureAwait(false);
            var locations = await _locationService.QueryAsync().ConfigureAwait(false);

            byte[] reviewBytes = _codec.SerializeReview(review);
            byte[] locationBytes = _codec.SerializeLocations(locations);

            return new SyncLocalSnapshot
            {
                Diaries = await CaptureDiariesAsync(ct).ConfigureAwait(false),
                Review = new Dictionary<string, SyncObjectMeta>(StringComparer.Ordinal)
                {
                    [SyncReviewTables.AllKey] = new SyncObjectMeta
                    {
                        UpdateTime = SyncReviewMerger.MaxUpdateTime(review),
                        Hash = _codec.Hash(reviewBytes),
                    },
                },
                Locations = locations.Count == 0
                    ? null
                    : new SyncObjectMeta
                    {
                        UpdateTime = SyncReviewMerger.MaxOf(locations),
                        Hash = _codec.Hash(locationBytes),
                    },
                Assets = await CaptureAssetsAsync(ct).ConfigureAwait(false),
                Graves = await CaptureGravesAsync().ConfigureAwait(false),
            };
        }

        private async Task<Dictionary<string, SyncObjectMeta>> CaptureDiariesAsync(CancellationToken ct)
        {
            // QueryAsync 包含模板：模板也是用户数据，换设备后应该还在。
            var diaries = await _diaryService.QueryAsync().ConfigureAwait(false);
            var result = new Dictionary<string, SyncObjectMeta>(StringComparer.Ordinal);

            // 当前是"每轮全量序列化 + 哈希"。几千篇日记时这是固定成本，
            // 但实现简单且不会漏改动；等实测同步变慢再用 UpdateTime 做筛选。
            foreach (var diary in diaries)
            {
                ct.ThrowIfCancellationRequested();

                byte[] bytes = _codec.SerializeDiary(diary);
                result[diary.Id.ToString("N")] = new SyncObjectMeta
                {
                    // 老数据的 UpdateTime 可能是 default，用 CreateTime 兜底比用公元 1 年合理。
                    UpdateTime = SyncTime.ToUnixMs(
                        diary.UpdateTime == default ? diary.CreateTime : diary.UpdateTime),
                    Hash = _codec.Hash(bytes),
                };
            }

            return result;
        }

        private Task<Dictionary<string, SyncObjectMeta>> CaptureAssetsAsync(CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                var result = new Dictionary<string, SyncObjectMeta>(StringComparer.Ordinal);

                foreach (var (type, directory) in _mediaResourceManager.MediaResourceDirectoryPaths)
                {
                    if (!Directory.Exists(directory))
                    {
                        continue;
                    }

                    foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        ct.ThrowIfCancellationRequested();

                        string key = SyncAssetPath.ToBareKey(type, directory, file);

                        // 默认配置下附件就是 SHA256 命名的（MediaResourceManager.cs），
                        // 直接取文件名能省掉"每次同步把附件全量读一遍"。
                        // 只有开启 OriginalFileName 时才需要真正读文件算哈希。
                        if (!SyncAssetPath.TryGetHashFromFileName(file, out string hash))
                        {
                            await using var stream = File.OpenRead(file);
                            byte[] bytes = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
                            hash = Convert.ToHexString(bytes).ToLowerInvariant();
                        }

                        result[key] = new SyncObjectMeta
                        {
                            UpdateTime = SyncTime.ToUnixMs(File.GetLastWriteTimeUtc(file)),
                            Hash = hash,
                        };
                    }
                }

                return result;
            }, ct);
        }

        private async Task<Dictionary<string, long>> CaptureGravesAsync()
        {
            var graves = await _graveService.QueryAsync().ConfigureAwait(false);

            return graves
                .Where(it => !string.IsNullOrEmpty(it.EntityKey))
                .GroupBy(it => it.EntityKey, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => SyncTime.ToUnixMs(group.Max(it => it.DeletedTime)),
                    StringComparer.Ordinal);
        }
    }
}
