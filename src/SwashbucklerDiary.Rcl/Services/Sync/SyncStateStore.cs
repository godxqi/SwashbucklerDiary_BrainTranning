using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwashbucklerDiary.Rcl.Services
{
    public class SyncStateStore : ISyncStateStore
    {
        private readonly ISettingService _settingService;

        private readonly ILogger<SyncStateStore> _logger;

        public SyncStateStore(ISettingService settingService, ILogger<SyncStateStore> logger)
        {
            _settingService = settingService;
            _logger = logger;
        }

        public async Task<SyncState> GetAsync()
        {
            string? json = await _settingService.GetAsync<string>(SyncKeys.StateKey, string.Empty)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new SyncState();
            }

            try
            {
                return JsonSerializer.Deserialize(json, SyncJsonContext.Default.SyncState)
                    ?? new SyncState();
            }
            catch (Exception ex)
            {
                // 状态损坏不应该让 App 打不开：当作从未同步过，下一次同步会重新建立状态。
                // 但必须留下日志——静默重置意味着下一轮同步会退化成全量。
                _logger.LogWarning(ex, "Failed to read sync state; resetting state");
                return new SyncState();
            }
        }

        public Task SaveAsync(SyncState state)
        {
            string json = JsonSerializer.Serialize(state, SyncJsonContext.Default.SyncState);
            return _settingService.SetAsync(SyncKeys.StateKey, json);
        }
    }
}
