using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 第 0 节的三个宏观方向（需求 11）。它们是全系统的锚：
    /// 没有锚，周目标 SMART 里那个 R（和更大方向一致吗？）就无从判断。
    /// </summary>
    public partial class MacroGoalPage : ImportantComponentBase
    {
        private List<MacroGoalModel> goals = [];

        private bool showEdit;

        private MacroGoalModel? editingGoal;

        private bool showDelete;

        private MacroGoalModel? pendingDelete;

        [Inject]
        private IMacroGoalService MacroGoalService { get; set; } = default!;

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
            goals = await MacroGoalService.QueryAsync();

            await InvokeAsync(StateHasChanged);
        }

        private void OpenEdit(MacroGoalModel? goal)
        {
            editingGoal = goal;
            showEdit = true;
        }

        private async Task SaveAsync(MacroGoalModel goal)
        {
            showEdit = false;

            if (!await MacroGoalService.SaveAsync(goal))
            {
                await AlertService.ErrorAsync(I18n.T("Change failed"));
                return;
            }

            await LoadAsync();
        }

        private void AskDelete(MacroGoalModel goal)
        {
            pendingDelete = goal;
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

            if (!await MacroGoalService.DeleteAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            goals.RemoveAll(it => it.Id == target.Id);

            await InvokeAsync(StateHasChanged);
        }

        private string DomainText(GoalDomain? domain) => ReviewSectionText.DomainText(I18n, domain);
    }
}
