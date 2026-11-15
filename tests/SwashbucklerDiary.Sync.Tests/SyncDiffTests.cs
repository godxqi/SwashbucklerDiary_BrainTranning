using SwashbucklerDiary.Rcl.Services;
using Xunit;

namespace SwashbucklerDiary.Sync.Tests
{
    public class SyncDiffTests
    {
        private static readonly DateTime Base = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Local);

        private static SyncObjectMeta Meta(DateTime time, string hash)
            => new() { UpdateTime = SyncTime.ToUnixMs(time), Hash = hash };

        private static string DiaryKey(Guid id) => SyncEntityKeys.Diary(id);

        private static SyncChange SingleChange(SyncPlan plan)
            => Assert.Single(plan.Changes);

        // ---- 基础方向 ----

        [Fact]
        public void RemoteNull_UploadsEverythingLocal()
        {
            var id = Guid.NewGuid();
            var local = new SyncLocalSnapshot
            {
                Diaries = { [id.ToString("N")] = Meta(Base, "h1") },
            };

            var plan = SyncDiff.Compute(null, local, Base);

            var change = SingleChange(plan);
            Assert.Equal(SyncAction.Upload, change.Action);
            Assert.Equal(SyncEntityType.Diary, change.EntityType);
        }

        [Fact]
        public void LocalEmpty_DownloadsEverythingRemote()
        {
            var id = Guid.NewGuid();
            var remote = new SyncManifest
            {
                Diaries = { [id.ToString("N")] = Meta(Base, "h1") },
            };

            var plan = SyncDiff.Compute(remote, new SyncLocalSnapshot(), Base);

            Assert.Equal(SyncAction.Download, SingleChange(plan).Action);
        }

        [Fact]
        public void SameHash_NoChange()
        {
            var id = Guid.NewGuid();
            var key = id.ToString("N");
            var meta = Meta(Base, "same");

            var plan = SyncDiff.Compute(
                new SyncManifest { Diaries = { [key] = meta } },
                new SyncLocalSnapshot { Diaries = { [key] = meta } },
                Base);

            Assert.Empty(plan.Changes);
        }

        // ---- LWW 与冲突 ----

        [Fact]
        public void LocalNewerOutsideWindow_Uploads()
        {
            var id = Guid.NewGuid();
            var key = id.ToString("N");

            var plan = SyncDiff.Compute(
                new SyncManifest { Diaries = { [key] = Meta(Base.AddMinutes(-10), "old") } },
                new SyncLocalSnapshot { Diaries = { [key] = Meta(Base.AddMinutes(-5), "new") } },
                Base);

            Assert.Equal(SyncAction.Upload, SingleChange(plan).Action);
        }

        [Fact]
        public void RemoteNewerOutsideWindow_Downloads()
        {
            var id = Guid.NewGuid();
            var key = id.ToString("N");

            var plan = SyncDiff.Compute(
                new SyncManifest { Diaries = { [key] = Meta(Base.AddMinutes(-5), "new") } },
                new SyncLocalSnapshot { Diaries = { [key] = Meta(Base.AddMinutes(-10), "old") } },
                Base);

            Assert.Equal(SyncAction.Download, SingleChange(plan).Action);
        }

        [Fact]
        public void BothChangedInsideWindow_Conflict()
        {
            var id = Guid.NewGuid();
            var key = id.ToString("N");

            var plan = SyncDiff.Compute(
                new SyncManifest { Diaries = { [key] = Meta(Base.AddMinutes(2), "remote") } },
                new SyncLocalSnapshot { Diaries = { [key] = Meta(Base.AddMinutes(1), "local") } },
                Base);

            Assert.Equal(SyncAction.Conflict, SingleChange(plan).Action);
        }

        [Fact]
        public void FirstSync_SameIdDifferentHash_Conflict()
        {
            var id = Guid.NewGuid();
            var key = id.ToString("N");

            var plan = SyncDiff.Compute(
                new SyncManifest { Diaries = { [key] = Meta(Base, "remote") } },
                new SyncLocalSnapshot { Diaries = { [key] = Meta(Base.AddMinutes(1), "local") } },
                default);

            Assert.Equal(SyncAction.Conflict, SingleChange(plan).Action);
        }

        // ---- 墓碑 ----

        [Fact]
        public void RemoteGraveNewerThanLocal_DeletesLocal()
        {
            var id = Guid.NewGuid();
            var key = DiaryKey(id);

            var remote = new SyncManifest();
            remote.Graves[key] = SyncTime.ToUnixMs(Base.AddMinutes(1));

            var local = new SyncLocalSnapshot
            {
                Diaries = { [id.ToString("N")] = Meta(Base, "h1") },
            };

            var plan = SyncDiff.Compute(remote, local, Base);

            Assert.Equal(SyncAction.DeleteLocal, SingleChange(plan).Action);
        }

        [Fact]
        public void RemoteGraveOlderThanLocal_KeepsLocalAndWithdrawsGrave()
        {
            var id = Guid.NewGuid();
            var key = DiaryKey(id);

            var remote = new SyncManifest();
            remote.Graves[key] = SyncTime.ToUnixMs(Base);

            var local = new SyncLocalSnapshot
            {
                Diaries = { [id.ToString("N")] = Meta(Base.AddMinutes(5), "h1") },
            };

            var plan = SyncDiff.Compute(remote, local, Base);

            var change = SingleChange(plan);
            Assert.Equal(SyncAction.Upload, change.Action);
            Assert.Contains(key, plan.GravesToWithdraw);
        }

        [Fact]
        public void LocalGraveInsideWindow_PublishesAndDeletesRemote()
        {
            var id = Guid.NewGuid();
            var key = DiaryKey(id);

            var local = new SyncLocalSnapshot();
            local.Graves[key] = SyncTime.ToUnixMs(Base.AddMinutes(1));

            var remote = new SyncManifest
            {
                Diaries = { [id.ToString("N")] = Meta(Base, "h1") },
            };

            var plan = SyncDiff.Compute(remote, local, Base);

            Assert.Contains(plan.GravesToPublish, entry => entry.Key == key);
            Assert.Equal(SyncAction.DeleteRemote, SingleChange(plan).Action);
        }

        [Fact]
        public void LocalGraveBeforeLastSync_IsIgnored()
        {
            var id = Guid.NewGuid();
            var key = DiaryKey(id);

            var local = new SyncLocalSnapshot();
            local.Graves[key] = SyncTime.ToUnixMs(Base.AddMinutes(-1));

            var plan = SyncDiff.Compute(new SyncManifest(), local, Base);

            Assert.Empty(plan.Changes);
            Assert.Empty(plan.GravesToPublish);
        }

        // ---- 其他实体 ----

        [Fact]
        public void ReviewTable_DiffsAsWholeTable()
        {
            var remote = new SyncManifest
            {
                Review = { ["daily"] = Meta(Base, "r1") },
            };

            var plan = SyncDiff.Compute(remote, new SyncLocalSnapshot(), Base);

            var change = SingleChange(plan);
            Assert.Equal(SyncEntityType.Review, change.EntityType);
            Assert.Equal(SyncEntityKeys.Review("daily"), change.Key);
            Assert.Equal(SyncAction.Download, change.Action);
        }

        [Fact]
        public void Location_DiffsAsSingleObject()
        {
            var local = new SyncLocalSnapshot
            {
                Locations = Meta(Base, "loc"),
            };

            var plan = SyncDiff.Compute(null, local, Base);

            var change = SingleChange(plan);
            Assert.Equal(SyncEntityType.Location, change.EntityType);
            Assert.Equal(SyncEntityKeys.LocationKey, change.Key);
            Assert.Equal(SyncAction.Upload, change.Action);
        }

        [Fact]
        public void Asset_UsesContentPathKey()
        {
            const string path = "Image/abc123.jpg";
            var remote = new SyncManifest
            {
                Assets = { [path] = Meta(Base, "a1") },
            };

            var plan = SyncDiff.Compute(remote, new SyncLocalSnapshot(), Base);

            var change = SingleChange(plan);
            Assert.Equal(SyncEntityType.Asset, change.EntityType);
            Assert.Equal(SyncEntityKeys.Asset(path), change.Key);
        }
    }
}
