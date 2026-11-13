using Masa.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Extensions;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;
using System.Runtime.CompilerServices;

namespace SwashbucklerDiary.Rcl.Components
{
    public abstract class MyComponentBase : ComponentBase, IAsyncDisposable
    {
        private readonly PropertyWatcher _watcher;

        protected bool IsDisposed { get; private set; }

        public MyComponentBase()
        {
            _watcher = new PropertyWatcher(GetType());
        }

        /// <summary>
        /// 复盘各页的芯片选中态样式。原先在 10 个页面里各写了一遍（页面与对话框都有），
        /// 放在共同基类上，两边的子类都能直接用，不必各留一份。
        /// </summary>
        protected static string ChipClass(bool isOn)
        {
            return isOn ? "review-chip review-chip--on" : "review-chip";
        }

        [Inject]
        protected IJSRuntime JS { get; set; } = default!;

        [Inject]
        protected INavigateController NavigateController { get; set; } = default!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = default!;

        [Inject]
        protected II18nService I18n { get; set; } = default!;

        [Inject]
        protected IAlertService AlertService { get; set; } = default!;

        [Inject]
        protected IAchievementService AchievementService { get; set; } = default!;

        [Inject]
        protected ISettingService SettingService { get; set; } = default!;

        [CascadingParameter(Name = "Culture")]
        public string? Culture { get; set; }

        protected override void OnAfterRender(bool firstRender)
        {
            base.OnAfterRender(firstRender);

            if (firstRender)
            {
                RegisterWatchers(_watcher);
            }
        }

        protected Task ToDo()
        {
            return AlertService.InfoAsync(I18n.T("This feature is currently unavailable"), I18n.T("Please look forward"));
        }

        protected void To(string url, bool cacheCurrentURL = true, bool replace = false)
        {
            if (!cacheCurrentURL)
            {
                NavigateController.RemovePageCache(NavigationManager.Uri);
            }

            NavigationManager.NavigateTo(url, new NavigationOptions()
            {
                ReplaceHistoryEntry = replace,
                HistoryEntryState = replace ? "replace" : null
            });
        }

        protected virtual async Task HandleAchievements(Achievement type)
        {
            var messages = await AchievementService.UpdateUserState(type);
            await AlertAchievements(messages);
        }

        protected async Task AlertAchievements(List<string> messages)
        {
            bool achievementsAlert = SettingService.Get(s => s.AchievementsAlert);
            if (!achievementsAlert)
            {
                return;
            }

            foreach (var item in messages)
            {
                await AlertService.InfoAsync(I18n.T("Achieving Achievements"), I18n.T(item));
            }
        }

        /// <summary>
        /// Register watchers at the first render.
        /// </summary>
        /// <param name="watcher"></param>
        protected virtual void RegisterWatchers(PropertyWatcher watcher)
        {
        }

        protected TValue? GetValue<TValue>(TValue? @default = default, [CallerMemberName] string name = "")
        {
            return _watcher.GetValue(@default, name);
        }

        protected TValue? GetComputedValue<TValue>(Func<TValue> valueFactory, string[] dependencyProperties,
            [CallerMemberName] string name = "")
        {
            return _watcher.GetComputedValue(valueFactory, dependencyProperties, name);
        }

        protected void SetValue<TValue>(TValue value, [CallerMemberName] string name = "",
            bool disableIListAlwaysNotifying = false)
        {
            _watcher.SetValue(value, name, disableIListAlwaysNotifying);
        }

        protected void SetValueWithNoEffect<TValue>(TValue? value, string name)
        {
            var property = _watcher.GetOrSetProperty<TValue>(default, name);
            property?.SetValueWithNoEffect(value);
        }

        protected virtual ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                await DisposeAsyncCore().ConfigureAwait(false);
            }
            catch (JSDisconnectedException)
            {
                // ignored
            }
            // HACK: remove this after https://github.com/dotnet/aspnetcore/issues/52119 is fixed
            catch (JSException e) when (e.Message.Contains("has it been disposed") && !OperatingSystem.IsBrowser())
            {
                // ignored
            }
            catch (InvalidOperationException e) when (e.Message.Contains("prerendering"))
            {
                // ignored
            }

            GC.SuppressFinalize(this);
            IsDisposed = true;
        }
    }
}
