using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 周课 2.3 的目标卡：领域 + 目标 + 挂靠宏观方向 + SMART 五问。
    /// SMART 的 A（可实现的）与 R（相关的）书里都要求对着更大方向答，所以宏观方向不是装饰。
    /// </summary>
    public partial class WeeklyGoalEditDialog : DialogComponentBase
    {
        private static readonly GoalDomain[] domains = Enum.GetValues<GoalDomain>();

        /// <summary>S/M/A/R/T 五问。与周课 2.1 的「未达成归因」共用同一份（见 ReviewSectionText），
        /// 免得改了问题文字只改一处。</summary>
        private string[] SmartKeys => ReviewSectionText.WeeklySmartKeys(I18n);

        private WeeklyGoalModel goal = new();

        private string[] smartValues = new string[ReviewSectionText.WeeklySmartLetters.Length];

        private List<MacroGoalModel> macroGoals = [];

        [Parameter]
        public WeeklyGoalModel? Value { get; set; }

        [Parameter]
        public Guid WeeklyReviewId { get; set; }

        [Parameter]
        public EventCallback<WeeklyGoalModel> OnOK { get; set; }

        [Inject]
        private IMacroGoalService MacroGoalService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            macroGoals = await MacroGoalService.GetActiveAsync();

            await base.OnInitializedAsync();
        }

        protected string DomainText(GoalDomain domain) => ReviewSectionText.DomainText(I18n, domain);

        /// <summary>宏观方向是可选的，再点一下取消挂靠。</summary>
        private void ToggleMacroGoal(Guid id)
        {
            goal.MacroGoalId = goal.MacroGoalId == id ? null : id;
        }

        private void HandleOnBeforeShowContent()
        {
            goal = Value is null
                ? new WeeklyGoalModel { WeeklyReviewId = WeeklyReviewId, Domain = GoalDomain.PersonalDevelopment }
                : new WeeklyGoalModel
                {
                    Id = Value.Id,
                    CreateTime = Value.CreateTime,
                    WeeklyReviewId = Value.WeeklyReviewId == Guid.Empty ? WeeklyReviewId : Value.WeeklyReviewId,
                    Domain = Value.Domain,
                    Content = Value.Content,
                    MacroGoalId = Value.MacroGoalId,
                    Status = Value.Status,
                    SettlementNote = Value.SettlementNote,
                    SettledYear = Value.SettledYear,
                    SettledWeekOfYear = Value.SettledWeekOfYear,
                };

            smartValues =
            [
                goal.SmartSpecific ?? string.Empty,
                goal.SmartMeasurable ?? string.Empty,
                goal.SmartAchievable ?? string.Empty,
                goal.SmartRelevant ?? string.Empty,
                goal.SmartTimeBound ?? string.Empty,
            ];
        }

        private async Task HandleOnOK()
        {
            if (string.IsNullOrWhiteSpace(goal.Content))
            {
                await AlertService.WarningAsync(I18n.T("Please enter content"));
                return;
            }

            //SMART 全空就存 null，免得库里躺着一堆空串
            goal.SmartSpecific = NullIfBlank(smartValues[0]);
            goal.SmartMeasurable = NullIfBlank(smartValues[1]);
            goal.SmartAchievable = NullIfBlank(smartValues[2]);
            goal.SmartRelevant = NullIfBlank(smartValues[3]);
            goal.SmartTimeBound = NullIfBlank(smartValues[4]);
            goal.Content = goal.Content!.Trim();

            if (OnOK.HasDelegate)
            {
                await OnOK.InvokeAsync(goal);
            }
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
