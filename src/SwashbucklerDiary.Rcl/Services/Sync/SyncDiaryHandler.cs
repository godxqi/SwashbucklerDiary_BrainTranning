using Microsoft.Extensions.Logging;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncDiaryHandler : ISyncDiaryHandler
    {
        private readonly IDiaryService _diaryService;

        private readonly ISyncStorage _storage;

        private readonly ISyncObjectCodec _codec;

        public SyncDiaryHandler(
            IDiaryService diaryService,
            ISyncStorage storage,
            ISyncObjectCodec codec)
        {
            _diaryService = diaryService;
            _storage = storage;
            _codec = codec;
        }

        public async Task ExecuteAsync(
            SyncChange change,
            SyncManifest remote,
            SyncState state,
            SyncExecuteResult result,
            CancellationToken ct)
        {
            string idText = SyncEntityKeys.GetBareKey(change.Key);
            if (!Guid.TryParseExact(idText, "N", out Guid id))
            {
                throw new ArgumentException($"Invalid diary key: {change.Key}");
            }

            switch (change.Action)
            {
                case SyncAction.Upload:
                    await UploadAsync(id, idText, remote, result, ct).ConfigureAwait(false);
                    break;

                case SyncAction.Download:
                    await DownloadAsync(idText, createConflictCopy: false, state, ct).ConfigureAwait(false);
                    result.Downloaded++;
                    break;

                case SyncAction.Conflict:
                    await DownloadAsync(idText, createConflictCopy: true, state, ct).ConfigureAwait(false);
                    result.Conflicts++;
                    break;

                case SyncAction.DeleteLocal:
                    await DeleteLocalAsync(id, idText, remote, result, ct).ConfigureAwait(false);
                    break;

                case SyncAction.DeleteRemote:
                    await _storage.DeleteAsync(DiaryPath(idText), ct).ConfigureAwait(false);
                    remote.Diaries.Remove(idText);
                    result.DeletedRemote++;
                    break;
            }
        }

        private async Task UploadAsync(
            Guid id,
            string idText,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct)
        {
            var diary = await _diaryService.FindAsync(id).ConfigureAwait(false);
            if (diary is null)
            {
                // 快照采集之后被本地删除，本轮跳过；下一次同步会通过墓碑传播。
                return;
            }

            byte[] bytes = _codec.SerializeDiary(diary);
            using var stream = new MemoryStream(bytes, writable: false);
            await _storage.PutAsync(DiaryPath(idText), stream, null, ct).ConfigureAwait(false);

            remote.Diaries[idText] = new SyncObjectMeta
            {
                UpdateTime = SyncTime.ToUnixMs(diary.UpdateTime == default ? diary.CreateTime : diary.UpdateTime),
                Hash = _codec.Hash(bytes),
            };
            result.Uploaded++;
        }

        private async Task DownloadAsync(
            string idText,
            bool createConflictCopy,
            SyncState state,
            CancellationToken ct)
        {
            var file = await _storage.GetAsync(DiaryPath(idText), ct).ConfigureAwait(false);
            if (file is null)
            {
                return;
            }

            byte[] bytes;
            await using (file.Content)
            {
                using var buffer = new MemoryStream();
                await file.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
                bytes = buffer.ToArray();
            }

            var remoteDiary = _codec.DeserializeDiary(bytes);
            if (remoteDiary is null)
            {
                return;
            }

            if (createConflictCopy && Guid.TryParseExact(idText, "N", out Guid id))
            {
                var local = await _diaryService.FindAsync(id).ConfigureAwait(false);
                if (local is not null)
                {
                    await CreateConflictCopyAsync(local, state).ConfigureAwait(false);
                }
            }

            // 两个抑制都必要：
            // - UpdateTime：保留远端对象的原始时间，否则 LWW 失效；
            // - GraveRecording：应用远端对象不应产生本地墓碑。
            using (SyncContext.BeginSuppressUpdateTimeRefresh())
            using (SyncContext.BeginSuppressGraveRecording())
            {
                await _diaryService.ImportAsync([remoteDiary]).ConfigureAwait(false);
            }
        }

        private async Task DeleteLocalAsync(
            Guid id,
            string idText,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct)
        {
            using (SyncContext.BeginSuppressGraveRecording())
            {
                await _diaryService.DeleteAsync(id).ConfigureAwait(false);
            }

            remote.Diaries.Remove(idText);
            result.DeletedLocal++;
        }

        private async Task CreateConflictCopyAsync(DiaryModel local, SyncState state)
        {
            var now = DateTime.Now;
            string device = string.IsNullOrWhiteSpace(state.DeviceName)
                ? "另一台设备"
                : state.DeviceName;

            var copy = new DiaryModel
            {
                Id = Guid.NewGuid(),
                CreateTime = local.CreateTime,
                UpdateTime = now,
                Title = $"{local.Title}（冲突副本 {now:yyyy-MM-dd HH:mm}）",
                Content = $"> ⚠ 同步冲突时保留的版本，来自 {device}。\n\n{local.Content}",
                Mood = local.Mood,
                Weather = local.Weather,
                Location = local.Location,
                Top = false,
                Template = local.Template,
                Kind = local.Kind,
                Tags = local.Tags?
                    .Select(it => new TagModel
                    {
                        Id = it.Id,
                        CreateTime = it.CreateTime,
                        UpdateTime = it.UpdateTime,
                        Name = it.Name,
                        Kind = it.Kind,
                    })
                    .ToList(),
                Resources = local.Resources?
                    .Select(it => new ResourceModel
                    {
                        ResourceUri = it.ResourceUri,
                        ResourceType = it.ResourceType,
                    })
                    .ToList(),
            };

            await _diaryService.ImportAsync([copy]).ConfigureAwait(false);
        }

        private static string DiaryPath(string idText) => $"diaries/{idText}.json";
    }
}
