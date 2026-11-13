using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 触因（需求 12 / 方法文档 5.2）。它复用整条标签管线，只是把 <see cref="TagModel.Kind"/>
    /// 标成 <see cref="TagKind.Trigger"/>。
    /// <para>
    /// 有了这一页，「攒够 3 个同类触因 → 交给月课 3.1」这条回路现在就能端到端跑通，
    /// 不用等阶段 4 的状态管理表。那张表的完整表单会复用同一批数据，这里记的「一次」
    /// 就是一条只填了时间和触因的状态记录。
    /// </para>
    /// </summary>
    public partial class TriggerPage : ImportantComponentBase
    {
        private List<TriggerCount> triggers = [];

        private bool showAdd;

        private bool showDelete;

        private TriggerCount? pendingDelete;

        [Inject]
        private ITriggerService TriggerService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();

            await base.OnInitializedAsync();
        }

        protected override async Task OnResume()
        {
            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            triggers = await TriggerService.GetWithCountsAsync();

            await InvokeAsync(StateHasChanged);
        }

        private async Task AddAsync(string name)
        {
            showAdd = false;

            if (!await TriggerService.AddAsync(name ?? string.Empty))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            await LoadAsync();
        }

        /// <summary>记一次。这条回路之所以能自动计数，靠的就是这一次次点击。</summary>
        private async Task RecordAsync(TriggerCount item)
        {
            if (!await TriggerService.RecordOccurrenceAsync(item.Tag.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Add failed"));
                return;
            }

            await LoadAsync();
        }

        private void AskDelete(TriggerCount item)
        {
            pendingDelete = item;
            showDelete = true;
        }

        private async Task ConfirmDeleteAsync()
        {
            showDelete = false;

            var target = pendingDelete;
            pendingDelete = null;
            if (target is null)
            {
                return;
            }

            if (!await TriggerService.DeleteAsync(target.Tag.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            triggers.RemoveAll(it => it.Tag.Id == target.Tag.Id);

            await InvokeAsync(StateHasChanged);
        }
    }
}
