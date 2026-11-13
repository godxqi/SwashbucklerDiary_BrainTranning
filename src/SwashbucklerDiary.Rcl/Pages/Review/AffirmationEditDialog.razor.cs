using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 自我肯定库（方法文档 4）的一条：短语 + 来自哪。
    /// <para>
    /// 两处共用：月课 3.2 把 3.1 第三列的反对性陈述提炼成短语，以及肯定库页自己增删改。
    /// 3.2 那一路由 <c>IMonthlyReviewService.AddAffirmationAsync</c> 落库，
    /// 因为还要写上「这是哪一次月课产出的」——那是 3.2 闸门的依据。
    /// </para>
    /// </summary>
    public partial class AffirmationEditDialog : DialogComponentBase
    {
        private SelfAffirmationModel affirmation = new();

        [Parameter]
        public SelfAffirmationModel? Value { get; set; }

        /// <summary>「来自哪」为空时的默认值（3.2 会传进来那条限制性观念）。</summary>
        [Parameter]
        public string? DefaultSource { get; set; }

        [Parameter]
        public EventCallback<SelfAffirmationModel> OnOK { get; set; }

        private void HandleOnBeforeShowContent()
        {
            affirmation = Value is null
                ? new SelfAffirmationModel { Source = DefaultSource, IsActive = true }
                : new SelfAffirmationModel
                {
                    Id = Value.Id,
                    CreateTime = Value.CreateTime,
                    Content = Value.Content,
                    Source = Value.Source,
                    RepeatCount = Value.RepeatCount,
                    SourceMonthlyReviewId = Value.SourceMonthlyReviewId,
                    IsActive = Value.IsActive,
                };
        }

        private async Task HandleOnOK()
        {
            if (string.IsNullOrWhiteSpace(affirmation.Content))
            {
                await AlertService.WarningAsync(I18n.T("Please enter content"));
                return;
            }

            affirmation.Content = affirmation.Content!.Trim();
            affirmation.Source = string.IsNullOrWhiteSpace(affirmation.Source)
                ? null
                : affirmation.Source.Trim();

            if (OnOK.HasDelegate)
            {
                await OnOK.InvokeAsync(affirmation);
            }
        }
    }
}
