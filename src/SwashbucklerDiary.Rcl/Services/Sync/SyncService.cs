using Microsoft.Extensions.Logging;
using SwashbucklerDiary.Rcl.Essentials;
using System.Text;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 同步编排入口。
    /// <para>
    /// 执行细节（拉取/推送/冲突副本/删除）在 <see cref="ISyncExecutor"/>；
    /// 这里只负责状态机、门闩、manifest 提交与同步前备份。
    /// </para>
    /// </summary>
    public class SyncService : ISyncService
    {
        private const string ProbeFolder = ".probe";

        private const string ProbeFile = ProbeFolder + "/ping.txt";

        private const string BackupFolderName = "SyncBackups";

        private const int BackupKeepCount = 3;

        private readonly ISyncStorage _storage;

        private readonly ISyncManifestStore _manifestStore;

        private readonly ISyncSnapshotService _snapshotService;

        private readonly ISyncStateStore _stateStore;

        private readonly ISyncExecutor _executor;

        private readonly ISyncDiagnosticsService _diagnostics;

        private readonly ISyncGraveService _graveService;

        private readonly ISettingService _settingService;

        private readonly IDiaryFileManager _diaryFileManager;

        private readonly IAppFileSystem _appFileSystem;

        private readonly IPlatformIntegration _platformIntegration;

        private readonly ILogger<SyncService> _logger;

        /// <summary>
        /// 串行化同步与自检。手动按钮和启动时自动同步可能同时触发，
        /// 同时跑会把 <c>LastSessionStart</c> / <c>LastManifestHash</c> 交叉覆盖。
        /// </summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        public SyncService(
            ISyncStorage storage,
            ISyncManifestStore manifestStore,
            ISyncSnapshotService snapshotService,
            ISyncStateStore stateStore,
            ISyncExecutor executor,
            ISyncDiagnosticsService diagnostics,
            ISyncGraveService graveService,
            ISettingService settingService,
            IDiaryFileManager diaryFileManager,
            IAppFileSystem appFileSystem,
            IPlatformIntegration platformIntegration,
            ILogger<SyncService> logger)
        {
            _storage = storage;
            _manifestStore = manifestStore;
            _snapshotService = snapshotService;
            _stateStore = stateStore;
            _executor = executor;
            _diagnostics = diagnostics;
            _graveService = graveService;
            _settingService = settingService;
            _diaryFileManager = diaryFileManager;
            _appFileSystem = appFileSystem;
            _platformIntegration = platformIntegration;
            _logger = logger;
        }

        public async Task<SyncResult> SyncAsync(CancellationToken ct = default)
        {
            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return SyncResult.BusyResult();
            }

            try
            {
                var state = await _stateStore.GetAsync().ConfigureAwait(false);
                EnsureState(state);

                // 记录"开始"时间而不是结束：同步过程中新写的日记留给下一轮，
                // 既不会漏，也不会因为落在窗口内被重复推送。
                var sessionStart = DateTime.Now;

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    var remote = await _manifestStore.ReadAsync(ct).ConfigureAwait(false);

                    if (remote is not null && remote.Manifest.Schema != SyncManifest.CurrentSchema)
                    {
                        return SyncResult.Failed(
                            $"云端数据结构版本不一致（{remote.Manifest.Schema} ≠ {SyncManifest.CurrentSchema}），需要整库覆盖");
                    }

                    var local = await _snapshotService.CaptureAsync(ct).ConfigureAwait(false);
                    var manifest = remote?.Manifest ?? new SyncManifest();
                    var plan = SyncDiff.Compute(manifest, local, state.LastSessionStart);

                    // 附件开关默认关闭：坚果云免费版每月流量有限，文字同步不该被图片拖垮。
                    if (!await _settingService.GetAsync(SyncKeys.IncludeAssetsKey, false).ConfigureAwait(false))
                    {
                        plan.Changes.RemoveAll(it => it.EntityType == SyncEntityType.Asset);
                    }

                    if (!plan.HasChanges)
                    {
                        state.LastSuccessTime = DateTime.Now;
                        await _stateStore.SaveAsync(state).ConfigureAwait(false);
                        return SyncResult.NoChangesResult();
                    }

                    if (attempt == 0)
                    {
                        await BackupBeforeSyncAsync(ct).ConfigureAwait(false);
                    }

                    var execute = await _executor.ExecuteAsync(plan, manifest, local, state, ct).ConfigureAwait(false);

                    foreach (var grave in plan.GravesToPublish)
                    {
                        manifest.Graves[grave.Key] = grave.DeletedAt;
                    }

                    foreach (string key in plan.GravesToWithdraw)
                    {
                        manifest.Graves.Remove(key);
                    }

                    // 墓碑保留 90 天：足够覆盖长期离线设备，又不至于让 manifest 无限膨胀。
                    long graveThresholdMs = SyncTime.ToUnixMs(DateTime.Now.AddDays(-90));
                    manifest.Graves = manifest.Graves
                        .Where(it => it.Value >= graveThresholdMs)
                        .ToDictionary(it => it.Key, it => it.Value, StringComparer.Ordinal);

                    manifest.Schema = SyncManifest.CurrentSchema;
                    manifest.AppVersion = _platformIntegration.AppVersionString;
                    manifest.UpdatedAt = DateTime.UtcNow;
                    manifest.DeviceId = state.DeviceId;

                    var write = await _manifestStore.WriteAsync(manifest, remote?.ETag, ct).ConfigureAwait(false);
                    if (!write.PreconditionFailed)
                    {
                        try
                        {
                            await _graveService
                                .ClearOlderThanAsync(DateTime.Now.AddDays(-90))
                                .ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // 清理失败不该把已经成功的同步报成失败。
                            _logger.LogWarning(ex, "Sync grave cleanup failed");
                        }

                        state.LastSessionStart = sessionStart;
                        state.LastSuccessTime = DateTime.Now;
                        await _stateStore.SaveAsync(state).ConfigureAwait(false);
                        return SyncResult.Completed(execute);
                    }

                    // 412：另一台设备抢先提交。重新拉 manifest、重新 diff 再试一轮。
                    // 已上传的内容是幂等的（内容寻址 + 哈希一致），重复执行不会产生副作用。
                    _logger.LogInformation("Sync manifest CAS failed, retrying (attempt {Attempt})", attempt + 1);
                }

                return SyncResult.Failed("另一台设备刚刚提交过同步，请稍后重试");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed");
                return SyncResult.Failed(ex.Message);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<SyncProbeResult> ProbeAsync(CancellationToken ct = default)
        {
            var result = new SyncProbeResult();

            if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                result.Add("并发检查", false, "已有同步或自检正在进行");
                return result;
            }

            try
            {
                result.Steps.Add(await _diagnostics.ProbeUpdateTimeAopAsync(ct).ConfigureAwait(false));

                string content = $"probe-{DateTime.UtcNow:O}";
                string? etag = null;

                result.Steps.Add(await StepAsync("确保目录", ct, async () =>
                {
                    await _storage.EnsureDirectoryAsync(ProbeFolder, ct).ConfigureAwait(false);
                    return (true, ProbeFolder);
                }));

                result.Steps.Add(await StepAsync("写入文件", ct, async () =>
                {
                    var write = await PutTextAsync(ProbeFile, content, null, ct).ConfigureAwait(false);
                    return (write.IsSuccess, $"HTTP {write.StatusCode}");
                }));

                result.Steps.Add(await StepAsync("读回并取 ETag", ct, async () =>
                {
                    var file = await _storage.GetAsync(ProbeFile, ct).ConfigureAwait(false);
                    if (file is null)
                    {
                        return (false, "读回为空");
                    }

                    etag = file.ETag;
                    string actual = await ReadTextAsync(file.Content, ct).ConfigureAwait(false);
                    bool same = actual == content;
                    return (same && etag is not null, $"内容一致={same}, ETag={etag ?? "<空>"}");
                }));

                result.Steps.Add(await StepAsync("错误 If-Match 应返回 412", ct, async () =>
                {
                    var write = await PutTextAsync(ProbeFile, "should-not-write", "\"not-the-current-etag\"", ct)
                        .ConfigureAwait(false);
                    return (write.PreconditionFailed, $"HTTP {write.StatusCode}（期望 412）");
                }));

                result.Steps.Add(await StepAsync("正确 If-Match 应成功", ct, async () =>
                {
                    if (etag is null)
                    {
                        return (false, "没有可用 ETag");
                    }

                    var write = await PutTextAsync(ProbeFile, "overwritten", etag, ct).ConfigureAwait(false);
                    return (write.IsSuccess, $"HTTP {write.StatusCode}");
                }));

                result.Steps.Add(await StepAsync("删除文件", ct, async () =>
                {
                    await _storage.DeleteAsync(ProbeFile, ct).ConfigureAwait(false);
                    bool exists = await _storage.ExistsAsync(ProbeFile, ct).ConfigureAwait(false);
                    return (!exists, $"删除后仍存在={exists}");
                }));

                result.Steps.Add(await StepAsync("清理目录", ct, async () =>
                {
                    await _storage.DeleteAsync(ProbeFolder, ct).ConfigureAwait(false);
                    return (true, ProbeFolder);
                }));

                return result;
            }
            finally
            {
                _gate.Release();
            }
        }

        private static void EnsureState(SyncState state)
        {
            if (string.IsNullOrEmpty(state.DeviceId))
            {
                state.DeviceId = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrEmpty(state.DeviceName))
            {
                state.DeviceName = Environment.MachineName;
            }
        }

        /// <summary>
        /// 同步前备份当前库。备份失败不阻断同步——它是安全网，不是前置条件。
        /// </summary>
        private async Task BackupBeforeSyncAsync(CancellationToken ct)
        {
            try
            {
                string source = await _diaryFileManager.ExportDBAsync(copyResources: false).ConfigureAwait(false);
                if (!File.Exists(source))
                {
                    return;
                }

                string directory = Path.Combine(_appFileSystem.AppDataDirectory, BackupFolderName);
                Directory.CreateDirectory(directory);

                string target = Path.Combine(directory, $"before-sync-{DateTime.Now:yyyyMMddHHmmss}.zip");
                File.Copy(source, target, overwrite: true);
                File.Delete(source);

                foreach (string old in Directory.EnumerateFiles(directory, "before-sync-*.zip")
                             .OrderByDescending(it => it, StringComparer.Ordinal)
                             .Skip(BackupKeepCount))
                {
                    try
                    {
                        File.Delete(old);
                    }
                    catch
                    {
                        // 清理旧备份失败不影响本次同步。
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sync pre-backup failed");
            }
        }

        private async Task<SyncWriteResult> PutTextAsync(string path, string content, string? ifMatch, CancellationToken ct)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes, writable: false);
            return await _storage.PutAsync(path, stream, ifMatch, ct).ConfigureAwait(false);
        }

        private static async Task<string> ReadTextAsync(Stream stream, CancellationToken ct)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: false);
            return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
        }

        private static async Task<SyncProbeStep> StepAsync(
            string name,
            CancellationToken ct,
            Func<Task<(bool Passed, string Detail)>> action)
        {
            try
            {
                var (passed, detail) = await action().ConfigureAwait(false);
                return new SyncProbeStep(name, passed, detail);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new SyncProbeStep(name, false, $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
