namespace SwashbucklerDiary.Rcl.Services
{
    public interface ISyncDiaryHandler
    {
        Task ExecuteAsync(
            SyncChange change,
            SyncManifest remote,
            SyncState state,
            SyncExecuteResult result,
            CancellationToken ct);
    }

    public interface ISyncAssetHandler
    {
        Task ExecuteAsync(
            IReadOnlyList<SyncChange> changes,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct);
    }

    public interface ISyncReviewHandler
    {
        Task MergeReviewAsync(
            SyncLocalSnapshot local,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct);

        Task MergeLocationsAsync(
            SyncLocalSnapshot local,
            SyncManifest remote,
            SyncExecuteResult result,
            CancellationToken ct);
    }
}
