using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Hybird.Extensions;
using SwashbucklerDiary.Rcl.Pages;
using SwashbucklerDiary.Rcl.Services;
using System.Text.Json;

namespace SwashbucklerDiary.Rcl.Hybird.Layout
{
    public class MainLayoutBase : Rcl.Layout.MainLayoutBase
    {
        [Inject]
        protected RouteMatcher RouteMatcher { get; set; } = default!;

        [Inject]
        private ISyncService SyncService { get; set; } = default!;

        [Inject]
        private ISettingService SettingService { get; set; } = default!;

        [Inject]
        private IWebDAV WebDAVService { get; set; } = default!;

        [Inject]
        private ISyncStateStore SyncStateStore { get; set; } = default!;

        [Inject]
        private ILogger<MainLayoutBase> Logger { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            await InternalOnInitializedAsync();

            await UpdateDocumentProperty(I18n.Culture);

            // 不阻塞首屏：自动同步在后台跑，结果只写日志。
            _ = TryAutoSyncAsync();
        }

        private async Task TryAutoSyncAsync()
        {
            try
            {
                if (!await SettingService.GetAsync(SyncKeys.AutoOnStartKey, false).ConfigureAwait(false))
                {
                    return;
                }

                if (SettingService.GetTemp(it => it.PrivacyMode))
                {
                    return;
                }

                // 刚同步过就别再跑一遍全量快照——冷启动时序列化几千篇日记是要花钱的。
                var state = await SyncStateStore.GetAsync().ConfigureAwait(false);
                if (state.LastSuccessTime is DateTime last
                    && DateTime.Now - last < TimeSpan.FromMinutes(5))
                {
                    return;
                }

                if (!WebDAVService.Initialized)
                {
                    string json = SettingService.Get(it => it.WebDavConfig);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    var form = JsonSerializer.Deserialize(json, WebDavJsonContext.Default.WebDavConfigForm);
                    if (form?.ServerAddress is null || form.Account is null || form.Password is null)
                    {
                        return;
                    }

                    await WebDAVService.Set(form.ServerAddress, form.Account, form.Password);
                }

                var result = await SyncService.SyncAsync().ConfigureAwait(false);
                if (!result.Success)
                {
                    Logger.LogWarning("Auto sync failed: {Message}", result.Message);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Auto sync");
            }
        }

        protected override void HandleSchemeActivation(ActivationArguments args, bool replace)
        {
            string? uriString = args?.Data as string;
            if (RouteMatcher.CheckUrlScheme(uriString, out var path))
            {
                To(path.TrimStart('/'), replace: replace);
            }
        }
    }
}
