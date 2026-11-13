using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 月课 3.4 敌人表的一行（三列三行的那一行）：
    /// 目标 / 阻止我实现目标的行为 / 驱动行为的深层观念 / 本周可完成的具体操作。
    /// <para>
    /// 与 3.1 的区别书里讲得很清楚：<b>3.1 针对信念，这里针对目标</b>——
    /// 「更多的是关于扭转顽固的模式，而非设定新的意图」。
    /// </para>
    /// </summary>
    public partial class EnemyEditDialog : DialogComponentBase
    {
        private EnemyModel enemy = new();

        [Parameter]
        public EnemyModel? Value { get; set; }

        [Parameter]
        public Guid MonthlyReviewId { get; set; }

        [Parameter]
        public EventCallback<EnemyModel> OnOK { get; set; }

        private void HandleOnBeforeShowContent()
        {
            enemy = Value is null
                ? new EnemyModel { MonthlyReviewId = MonthlyReviewId }
                : new EnemyModel
                {
                    Id = Value.Id,
                    CreateTime = Value.CreateTime,
                    MonthlyReviewId = Value.MonthlyReviewId == Guid.Empty ? MonthlyReviewId : Value.MonthlyReviewId,
                    Goal = Value.Goal,
                    BlockingBehavior = Value.BlockingBehavior,
                    DeepBelief = Value.DeepBelief,
                    WeeklyAction = Value.WeeklyAction,
                };
        }

        private async Task HandleOnOK()
        {
            //三列的第一列是这张表的定位器，空的话整行就没有所指
            if (string.IsNullOrWhiteSpace(enemy.Goal))
            {
                await AlertService.WarningAsync(I18n.T("Please enter content"));
                return;
            }

            enemy.Goal = enemy.Goal!.Trim();
            enemy.BlockingBehavior = NullIfBlank(enemy.BlockingBehavior);
            enemy.DeepBelief = NullIfBlank(enemy.DeepBelief);
            enemy.WeeklyAction = NullIfBlank(enemy.WeeklyAction);

            if (OnOK.HasDelegate)
            {
                await OnOK.InvokeAsync(enemy);
            }
        }

        private static string? NullIfBlank(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
