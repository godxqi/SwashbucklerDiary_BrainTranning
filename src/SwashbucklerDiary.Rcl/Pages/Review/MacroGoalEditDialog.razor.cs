using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>第 0 节的宏观方向。领域是可选的——文档说「挂不上也没关系」。</summary>
    public partial class MacroGoalEditDialog : DialogComponentBase
    {
        private static readonly GoalDomain[] domains = Enum.GetValues<GoalDomain>();

        private MacroGoalModel goal = new();

        [Parameter]
        public MacroGoalModel? Value { get; set; }

        [Parameter]
        public EventCallback<MacroGoalModel> OnOK { get; set; }

        protected string DomainText(GoalDomain domain) => ReviewSectionText.DomainText(I18n, domain);

        private void ToggleDomain(GoalDomain domain)
        {
            goal.Domain = goal.Domain == domain ? null : domain;
        }

        private void HandleOnBeforeShowContent()
        {
            goal = Value is null
                ? new MacroGoalModel()
                : new MacroGoalModel
                {
                    Id = Value.Id,
                    CreateTime = Value.CreateTime,
                    Title = Value.Title,
                    Description = Value.Description,
                    Domain = Value.Domain,
                    IsActive = Value.IsActive,
                };
        }

        private async Task HandleOnOK()
        {
            if (string.IsNullOrWhiteSpace(goal.Title))
            {
                await AlertService.WarningAsync(I18n.T("Please enter content"));
                return;
            }

            goal.Title = goal.Title!.Trim();
            goal.Description = string.IsNullOrWhiteSpace(goal.Description) ? null : goal.Description.Trim();

            if (OnOK.HasDelegate)
            {
                await OnOK.InvokeAsync(goal);
            }
        }
    }
}
