using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 月课 3.1 三列审视的一行：限制性观念 + 支持性证据 + 反面证据 + 效用评估。
    /// 四格都留着空是允许的（可以只写第一列先记下来），但第一列必须有——
    /// 没有观念这一行就没有意义，闸门也算不上「有产出」。
    /// </summary>
    public partial class LimitingBeliefEditDialog : DialogComponentBase
    {
        private LimitingBeliefModel belief = new();

        [Parameter]
        public LimitingBeliefModel? Value { get; set; }

        [Parameter]
        public Guid MonthlyReviewId { get; set; }

        [Parameter]
        public EventCallback<LimitingBeliefModel> OnOK { get; set; }

        private void HandleOnBeforeShowContent()
        {
            belief = Value is null
                ? new LimitingBeliefModel { MonthlyReviewId = MonthlyReviewId }
                : new LimitingBeliefModel
                {
                    Id = Value.Id,
                    CreateTime = Value.CreateTime,
                    MonthlyReviewId = Value.MonthlyReviewId == Guid.Empty ? MonthlyReviewId : Value.MonthlyReviewId,
                    Assertion = Value.Assertion,
                    SupportingEvidence = Value.SupportingEvidence,
                    CounterEvidence = Value.CounterEvidence,
                    Utility = Value.Utility,
                };
        }

        private async Task HandleOnOK()
        {
            if (string.IsNullOrWhiteSpace(belief.Assertion))
            {
                await AlertService.WarningAsync(I18n.T("Please enter content"));
                return;
            }

            belief.Assertion = belief.Assertion!.Trim();
            belief.SupportingEvidence = NullIfBlank(belief.SupportingEvidence);
            belief.CounterEvidence = NullIfBlank(belief.CounterEvidence);
            belief.Utility = NullIfBlank(belief.Utility);

            if (OnOK.HasDelegate)
            {
                await OnOK.InvokeAsync(belief);
            }
        }

        /// <summary>空串存进去只会让「这格填没填」判断变复杂，统一收成 null。</summary>
        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
