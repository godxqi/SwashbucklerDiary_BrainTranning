using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 状态管理表（方法文档 5）的历史列表。新记的排前面；
    /// 触因页「＋ 记一次」留下的半成品在这里标着「待补全」，点进去把剩下的格子填上。
    /// </summary>
    public partial class StateRecordListPage : ImportantComponentBase
    {
        private List<StateRecordView> records = [];

        private bool showDelete;

        private StateRecordView? pendingDelete;

        [Inject]
        private IStateRecordService StateRecordService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();

            await base.OnInitializedAsync();
        }

        protected override async Task OnResume()
        {
            //从表单页回来时列表要反映刚存下的那条
            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            records = await StateRecordService.QueryAsync();

            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// 列表一行的两段文字。一次算好，免得主行与副行各算一遍还互相不知道对方写了什么。
        /// <para>
        /// 主行优先显示场景，没写场景就退到另一种描述；都没有（「记一次」留下的半成品）
        /// 才拿触因名顶上。触因名被主行用掉时，副行不再重复列一遍。
        /// </para>
        /// </summary>
        private (string Title, string Subtitle) TextOf(StateRecordView view)
        {
            var record = view.Record;
            string? written = new[]
            {
                record.Scene, record.Body, record.Mind, record.Emotion, record.Spirit, record.ActionNow,
            }.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            string title = written ?? view.TriggerName ?? I18n.T("To complete");

            var parts = new List<string> { record.OccurredTime.ToString("yyyy/MM/dd HH:mm") };

            if (written is not null && view.TriggerName is not null)
            {
                parts.Add(view.TriggerName);
            }

            if (record.EmotionName is MoodKind mood)
            {
                //八情绪的名字本来就有翻译，直接用
                parts.Add(I18n.T(mood.ToString()));
            }

            return (title, string.Join(" · ", parts));
        }

        private void AskDeleteAsync(StateRecordView view)
        {
            pendingDelete = view;
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

            if (!await StateRecordService.DeleteAsync(target.Record.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            records.RemoveAll(it => it.Record.Id == target.Record.Id);

            await InvokeAsync(StateHasChanged);
        }
    }
}
