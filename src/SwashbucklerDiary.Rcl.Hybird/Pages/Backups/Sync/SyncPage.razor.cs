using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Services;
using System.Text.Json;

namespace SwashbucklerDiary.Rcl.Pages
{
    public partial class SyncPage : ImportantComponentBase
    {
        private bool privacyMode;

        private bool includeAssets;

        private bool autoOnStart;

        private SyncState state = new();

        private bool showProbe;

        private List<SyncProbeStep> probeSteps = [];

        [Inject]
        private ISyncService SyncService { get; set; } = default!;

        [Inject]
        private ISyncStateStore SyncStateStore { get; set; } = default!;

        [Inject]
        private IWebDAV WebDAVService { get; set; } = default!;

        [Inject]
        private ILogger<SyncPage> Logger { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            privacyMode = SettingService.GetTemp(it => it.PrivacyMode);
            includeAssets = await SettingService.GetAsync(SyncKeys.IncludeAssetsKey, false);
            autoOnStart = await SettingService.GetAsync(SyncKeys.AutoOnStartKey, false);
            state = await SyncStateStore.GetAsync();
        }

        protected override async Task OnResume()
        {
            // 先读状态再交给基类：基类会触发 StateHasChanged，
            // 反过来写的话界面拿到的是刷新前的旧值。
            state = await SyncStateStore.GetAsync();
            await base.OnResume();
        }

        private string lastSyncText
            => state.LastSuccessTime is DateTime time
                ? $"{I18n.T("Last synced")}: {time:yyyy-MM-dd HH:mm}"
                : I18n.T("Not synced yet");

        private async Task RunSync()
        {
            AlertService.StartLoading();

            try
            {
                if (!await EnsureWebDavAsync())
                {
                    return;
                }

                var result = await SyncService.SyncAsync();
                state = await SyncStateStore.GetAsync();

                if (result.Busy)
                {
                    await AlertService.InfoAsync(result.Message ?? string.Empty);
                    return;
                }

                if (result.NoChanges)
                {
                    await AlertService.SuccessAsync(I18n.T("No changes to sync"));
                    return;
                }

                if (!result.Success)
                {
                    // 概要只说"N 个对象失败"，具体是哪个附件/哪篇日记要看错误列表。
                    string detail = result.Errors.Count > 0
                        ? "\n" + string.Join("\n", result.Errors.Take(3))
                        : string.Empty;

                    await AlertService.ErrorAsync($"{I18n.T("Sync failed")}: {result.Message}{detail}");
                    return;
                }

                string summary =
                    $"{I18n.T("Uploaded")} {result.Uploaded}, "
                    + $"{I18n.T("Downloaded")} {result.Downloaded}, "
                    + $"{I18n.T("Deleted")} {result.DeletedLocal + result.DeletedRemote}, "
                    + $"{I18n.T("Conflicts")} {result.Conflicts}";

                await AlertService.SuccessAsync(summary);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SyncPage.RunSync");
                await AlertService.ErrorAsync(ex.Message);
            }
            finally
            {
                AlertService.StopLoading();
            }
        }

        private async Task RunProbe()
        {
            AlertService.StartLoading();

            try
            {
                if (!await EnsureWebDavAsync())
                {
                    return;
                }

                var result = await SyncService.ProbeAsync();
                probeSteps = result.Steps;
                showProbe = true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SyncPage.RunProbe");
                await AlertService.ErrorAsync(ex.Message);
            }
            finally
            {
                AlertService.StopLoading();
            }
        }

        private async Task<bool> EnsureWebDavAsync()
        {
            if (WebDAVService.Initialized)
            {
                return true;
            }

            string json = SettingService.Get(it => it.WebDavConfig);
            if (string.IsNullOrWhiteSpace(json))
            {
                await AlertService.ErrorAsync(I18n.T("Configure WebDAV first"));
                return false;
            }

            try
            {
                var form = JsonSerializer.Deserialize(json, WebDavJsonContext.Default.WebDavConfigForm);
                if (form?.ServerAddress is null || form.Account is null || form.Password is null)
                {
                    await AlertService.ErrorAsync(I18n.T("Configure WebDAV first"));
                    return false;
                }

                await WebDAVService.Set(form.ServerAddress, form.Account, form.Password);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SyncPage.EnsureWebDav");
                await AlertService.ErrorAsync(
                    I18n.T("Configuration failed, please check the configuration information"));
                return false;
            }
        }
    }
}
