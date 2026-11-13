using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 自我肯定库（方法文档 4）。文档 4.1 说它「是一个 3–4 条的短语库，每天多次用心重复」。
    /// <para>
    /// 两个入口都通到这里：月课 3.2 把 3.1 第三列的反对性陈述提炼成短语，3.4 敌人表的打击手段①
    /// 用积极信念替代自我贬低的信念。复盘主页顶部的条负责「重复」，这里负责「库」本身。
    /// </para>
    /// <para>
    /// 文档 4.2 那张表有四列（短语 / 来自哪 / 用了几次），所以这三样都在行里显示出来。
    /// </para>
    /// </summary>
    public partial class AffirmationLibraryPage : ImportantComponentBase
    {
        /// <summary>文档 4.2：「我通常觉得三四个就足够了」。</summary>
        private const int SuggestedCount = 4;

        private List<SelfAffirmationModel> affirmations = [];

        private bool showEdit;

        private bool editing;

        private SelfAffirmationModel? editingAffirmation;

        private bool showDelete;

        private SelfAffirmationModel? pendingDelete;

        [Inject]
        private IAffirmationService AffirmationService { get; set; } = default!;

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
            affirmations = await AffirmationService.QueryAsync();

            await InvokeAsync(StateHasChanged);
        }

        private void OpenAdd()
        {
            editing = false;
            editingAffirmation = null;
            showEdit = true;
        }

        private void OpenEdit(SelfAffirmationModel affirmation)
        {
            editing = true;
            editingAffirmation = affirmation;
            showEdit = true;
        }

        private async Task SaveAsync(SelfAffirmationModel affirmation)
        {
            showEdit = false;

            if (editingAffirmation is null)
            {
                affirmation.Id = Guid.NewGuid();

                if (!await AffirmationService.AddAsync(affirmation))
                {
                    await AlertService.ErrorAsync(I18n.T("Add failed"));
                    return;
                }
            }
            else
            {
                editingAffirmation.Content = affirmation.Content;
                editingAffirmation.Source = affirmation.Source;

                if (!await AffirmationService.UpdateAsync(editingAffirmation))
                {
                    await AlertService.ErrorAsync(I18n.T("Change failed"));
                    return;
                }
            }

            await LoadAsync();
        }

        private void AskDelete(SelfAffirmationModel affirmation)
        {
            pendingDelete = affirmation;
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

            if (!await AffirmationService.DeleteAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            affirmations.RemoveAll(it => it.Id == target.Id);

            await InvokeAsync(StateHasChanged);
        }

        private string CountHint => $"{I18n.T("Affirmation library")} · {affirmations.Count}/{SuggestedCount}";

        /// <summary>用了几次。没重复过就直说，免得显示成 0 让人以为坏了。</summary>
        private string RepeatText(SelfAffirmationModel affirmation)
            => affirmation.RepeatCount == 0
                ? I18n.T("Not repeated yet")
                : $"{I18n.T("Repeated")} {affirmation.RepeatCount} ×";
    }
}
