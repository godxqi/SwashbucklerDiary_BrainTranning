using Masa.Blazor;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Models;

namespace SwashbucklerDiary.Rcl.Pages
{
    public abstract partial class BackupsPageBase : ImportantComponentBase
    {
        protected virtual Type? SyncPageType => null;

        protected List<TabListItem> tabListItems = [];

        protected override void OnInitialized()
        {
            base.OnInitialized();

            // 在生命周期里建一次，别让每次渲染都重新 new 列表。
            tabListItems =
            [
                new("Local backup", "local"),
                new("WebDAV", "webDAV"),
            ];

            // Web 版没有 WebDAV 同步存储实现，不展示这个 tab。
            if (SyncPageType is not null)
            {
                tabListItems.Add(new("Sync", "sync"));
            }
        }

        protected StringNumber tab = 0;

        protected abstract Type WebDAVBackupsPageType { get; }
    }
}
