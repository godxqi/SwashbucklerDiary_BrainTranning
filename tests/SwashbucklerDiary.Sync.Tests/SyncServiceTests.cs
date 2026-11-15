using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Services;
using Xunit;

namespace SwashbucklerDiary.Sync.Tests
{
    /// <summary>
    /// 用真实 <see cref="LocalSyncStorage"/> + 真实 manifest 读写，验证 <see cref="SyncService"/>
    /// 的编排、CAS 与状态机；执行器用假实现记录计划，不碰数据库。
    /// </summary>
    public class SyncServiceTests : IDisposable
    {
        private readonly string _root;

        private readonly LocalSyncStorage _storage;

        private readonly SyncManifestStore _manifestStore;

        private readonly FakeSnapshotService _snapshot = new();

        private readonly FakeStateStore _state = new();

        private readonly FakeExecutor _executor = new();

        public SyncServiceTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "sync-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _storage = new LocalSyncStorage(_root);
            _manifestStore = new SyncManifestStore(_storage);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch
            {
                // 测试清理失败不影响结果。
            }
        }

        private static SyncObjectMeta Meta(DateTime time, string hash)
            => new() { UpdateTime = SyncTime.ToUnixMs(time), Hash = hash };

        private SyncService CreateService(ISyncManifestStore? manifestStore = null)
        {
            var appFileSystem = new Mock<IAppFileSystem>();
            appFileSystem.SetupGet(it => it.AppDataDirectory).Returns(_root);

            var platform = new Mock<IPlatformIntegration>();
            platform.SetupGet(it => it.AppVersionString).Returns("test");

            var fileManager = new Mock<IDiaryFileManager>();

            var graveService = new Mock<ISyncGraveService>();

            var settingService = new Mock<ISettingService>();
            settingService
                .Setup(it => it.GetAsync(SyncKeys.IncludeAssetsKey, false))
                .ReturnsAsync(false);

            return new SyncService(
                _storage,
                manifestStore ?? _manifestStore,
                _snapshot,
                _state,
                _executor,
                new FakeDiagnostics(),
                graveService.Object,
                settingService.Object,
                fileManager.Object,
                appFileSystem.Object,
                platform.Object,
                NullLogger<SyncService>.Instance);
        }

        [Fact]
        public async Task FirstSync_UploadsAndCommitsManifest()
        {
            var id = Guid.NewGuid();
            _snapshot.Snapshot = new SyncLocalSnapshot
            {
                Diaries = { [id.ToString("N")] = Meta(DateTime.Now, "h1") },
            };

            var result = await CreateService().SyncAsync();

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, result.Uploaded);
            Assert.NotNull(_executor.LastPlan);

            var remote = await _manifestStore.ReadAsync();
            Assert.NotNull(remote);
            Assert.True(remote!.Manifest.Diaries.ContainsKey(id.ToString("N")));
            Assert.NotEqual(default, _state.State.LastSessionStart);
        }

        [Fact]
        public async Task SecondSync_WithUnchangedSnapshot_ReturnsNoChanges()
        {
            var id = Guid.NewGuid();
            _snapshot.Snapshot = new SyncLocalSnapshot
            {
                Diaries = { [id.ToString("N")] = Meta(DateTime.Now, "h1") },
            };

            var service = CreateService();
            await service.SyncAsync();

            var second = await service.SyncAsync();

            Assert.True(second.NoChanges);
            Assert.Equal(0, second.Uploaded);
        }

        [Fact]
        public async Task RemoteObject_DownloadsWhenMissingLocally()
        {
            var id = Guid.NewGuid();
            var key = id.ToString("N");
            var manifest = new SyncManifest
            {
                Diaries = { [key] = Meta(DateTime.Now, "remote") },
            };
            await _manifestStore.WriteAsync(manifest, null);

            var result = await CreateService().SyncAsync();

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, result.Downloaded);
        }

        [Fact]
        public async Task LocalGrave_DeletesRemoteAndPublishesGrave()
        {
            var id = Guid.NewGuid();
            var key = SyncEntityKeys.Diary(id);
            var bareKey = id.ToString("N");

            await _manifestStore.WriteAsync(new SyncManifest
            {
                Diaries = { [bareKey] = Meta(DateTime.Now.AddMinutes(-10), "old") },
            }, null);

            _snapshot.Snapshot = new SyncLocalSnapshot();
            _snapshot.Snapshot.Graves[key] = SyncTime.ToUnixMs(DateTime.Now);

            var result = await CreateService().SyncAsync();

            Assert.True(result.Success, result.Message);
            Assert.Equal(1, result.DeletedRemote);

            var remote = await _manifestStore.ReadAsync();
            Assert.NotNull(remote);
            Assert.True(remote!.Manifest.Graves.ContainsKey(key));
            Assert.False(remote.Manifest.Diaries.ContainsKey(bareKey));
        }

        [Fact]
        public async Task ManifestCasConflict_RetriesAndSucceeds()
        {
            var id = Guid.NewGuid();
            _snapshot.Snapshot = new SyncLocalSnapshot
            {
                Diaries = { [id.ToString("N")] = Meta(DateTime.Now, "h1") },
            };

            var flaky = new FlakyManifestStore(_manifestStore);
            var result = await CreateService(flaky).SyncAsync();

            Assert.True(result.Success, result.Message);
            Assert.Equal(2, flaky.WriteAttempts);
        }

        // ---- 测试替身 ----

        private sealed class FakeSnapshotService : ISyncSnapshotService
        {
            public SyncLocalSnapshot Snapshot { get; set; } = new();

            public Task<SyncLocalSnapshot> CaptureAsync(CancellationToken ct = default)
                => Task.FromResult(Snapshot);
        }

        private sealed class FakeStateStore : ISyncStateStore
        {
            public SyncState State { get; set; } = new();

            public Task<SyncState> GetAsync() => Task.FromResult(State);

            public Task SaveAsync(SyncState state)
            {
                State = state;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeExecutor : ISyncExecutor
        {
            public SyncPlan? LastPlan { get; private set; }

            public Task<SyncExecuteResult> ExecuteAsync(
                SyncPlan plan,
                SyncManifest remote,
                SyncLocalSnapshot local,
                SyncState state,
                CancellationToken ct = default)
            {
                LastPlan = plan;
                var result = new SyncExecuteResult();

                foreach (var change in plan.Changes)
                {
                    switch (change.Action)
                    {
                        case SyncAction.Upload:
                            // 真实执行器上传后会写回对象元数据，替身必须模拟，
                            // 否则下一轮 diff 仍认为远端缺失。
                            remote.Diaries[SyncEntityKeys.GetBareKey(change.Key)] = new SyncObjectMeta
                            {
                                UpdateTime = change.LocalUpdateTime,
                                // 与测试快照里的哈希保持一致；真实执行器写入的是本地内容哈希。
                                Hash = "h1",
                            };
                            result.Uploaded++;
                            break;
                        case SyncAction.Download:
                            result.Downloaded++;
                            break;
                        case SyncAction.DeleteLocal:
                            result.DeletedLocal++;
                            break;
                        case SyncAction.DeleteRemote:
                            remote.Diaries.Remove(SyncEntityKeys.GetBareKey(change.Key));
                            result.DeletedRemote++;
                            break;
                        case SyncAction.Conflict:
                            result.Conflicts++;
                            break;
                    }
                }

                return Task.FromResult(result);
            }
        }

        private sealed class FakeDiagnostics : ISyncDiagnosticsService
        {
            public Task<SyncProbeStep> ProbeUpdateTimeAopAsync(CancellationToken ct = default)
                => Task.FromResult(new SyncProbeStep("aop", true, "ok"));
        }

        /// <summary>第一次写入返回 412，模拟另一台设备抢先提交。</summary>
        private sealed class FlakyManifestStore : ISyncManifestStore
        {
            private readonly ISyncManifestStore _inner;

            public int WriteAttempts { get; private set; }

            public FlakyManifestStore(ISyncManifestStore inner)
            {
                _inner = inner;
            }

            public Task<SyncManifestSnapshot?> ReadAsync(CancellationToken ct = default)
                => _inner.ReadAsync(ct);

            public Task<SyncWriteResult> WriteAsync(SyncManifest manifest, string? ifMatch, CancellationToken ct = default)
            {
                WriteAttempts++;
                if (WriteAttempts == 1)
                {
                    return Task.FromResult(new SyncWriteResult { StatusCode = 412 });
                }

                return _inner.WriteAsync(manifest, ifMatch, ct);
            }
        }
    }
}
