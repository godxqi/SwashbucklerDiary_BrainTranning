using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;
using Xunit;

namespace SwashbucklerDiary.Sync.Tests
{
    public class SyncReviewMergerTests
    {
        private static DailyReviewModel Row(Guid id, DateTime time, string note = "")
            => new() { Id = id, UpdateTime = time, AffirmationIds = note };

        [Fact]
        public void DifferentRowsOnBothSides_AreBothKept()
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();

            var merged = SyncReviewMerger.MergeRows(
                [Row(a, DateTime.Now.AddMinutes(-5))],
                [Row(b, DateTime.Now.AddMinutes(-3))],
                "daily",
                new Dictionary<string, long>());

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void SameRow_NewerWins()
        {
            var id = Guid.NewGuid();
            var older = Row(id, DateTime.Now.AddMinutes(-5), "old");
            var newer = Row(id, DateTime.Now.AddMinutes(-1), "new");

            var merged = SyncReviewMerger.MergeRows(
                [older],
                [newer],
                "daily",
                new Dictionary<string, long>());

            Assert.Single(merged);
            Assert.Equal("new", merged[0].AffirmationIds);
        }

        [Fact]
        public void RowGrave_DeletesRow()
        {
            var id = Guid.NewGuid();
            var row = Row(id, DateTime.Now.AddMinutes(-5));

            var graves = new Dictionary<string, long>
            {
                [$"{SyncEntityKeys.ReviewPrefix}daily:{id:N}"] = SyncTime.ToUnixMs(DateTime.Now),
            };

            var merged = SyncReviewMerger.MergeRows([row], [], "daily", graves);

            Assert.Empty(merged);
        }

        [Fact]
        public void RowGraveOlderThanEdit_KeepsRow()
        {
            var id = Guid.NewGuid();
            var row = Row(id, DateTime.Now.AddMinutes(5));

            var graves = new Dictionary<string, long>
            {
                [$"{SyncEntityKeys.ReviewPrefix}daily:{id:N}"] = SyncTime.ToUnixMs(DateTime.Now),
            };

            var merged = SyncReviewMerger.MergeRows([row], [], "daily", graves);

            Assert.Single(merged);
        }

        [Fact]
        public void LocationGrave_UsesLocationPrefix()
        {
            var id = Guid.NewGuid();
            var row = new LocationModel("家") { Id = id, UpdateTime = DateTime.Now.AddMinutes(-5) };

            var graves = new Dictionary<string, long>
            {
                [SyncEntityKeys.LocationPrefix + id.ToString("N")] = SyncTime.ToUnixMs(DateTime.Now),
            };

            var merged = SyncReviewMerger.MergeRows(
                [row], [], "location", graves, SyncEntityKeys.LocationPrefix);

            Assert.Empty(merged);
        }

        [Fact]
        public void Merge_ProducesStableOrdering()
        {
            var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToList();
            var rows = ids.Select(id => Row(id, DateTime.Now)).ToList();

            var first = SyncReviewMerger.MergeRows(rows, [], "daily", new Dictionary<string, long>());
            var reversed = SyncReviewMerger.MergeRows(
                Enumerable.Reverse(rows).ToList(), [], "daily", new Dictionary<string, long>());

            Assert.Equal(first.Select(it => it.Id), reversed.Select(it => it.Id));
        }
    }
}
