namespace SwashbucklerDiary.Rcl.Services
{
    public interface ISyncStateStore
    {
        Task<SyncState> GetAsync();

        Task SaveAsync(SyncState state);
    }
}
