using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Models;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Components
{
    public partial class ExportDialog : DialogComponentBase
    {
        private List<DynamicListItem> items = [];

        [Inject]
        protected IDiaryFileManager DiaryFileManager { get; set; } = default!;

        [Inject]
        private ILogger<ExportDialog> Logger { get; set; } = default!;

        [Inject]
        private IPlatformIntegration PlatformIntegration { get; set; } = default!;

        [Parameter]
        public List<DiaryModel> Value { get; set; } = [];

        protected override void OnInitialized()
        {
            base.OnInitialized();
            LoadView();
        }

        private void LoadView()
        {
            items =
            [
                new(this,"Txt", "description", ExportTxtAsync),
                new(this,"Md", "markdown", ExportMdAsync),
                new(this,"Json", "file_json", ExportJsonAsync),
                new(this,"Xlsx", "mdi:mdi-microsoft-excel", ExportXlsxAsync),
            ];
        }

        private Task ExportTxtAsync() => Export(DiaryFileManager.ExportTxtAsync, ExportKind.Txt);

        private Task ExportJsonAsync() => Export(DiaryFileManager.ExportJsonAsync, ExportKind.Json, JsonNote);

        private Task ExportMdAsync() => Export(DiaryFileManager.ExportMdAsync, ExportKind.Md);

        private Task ExportXlsxAsync() => Export(DiaryFileManager.ExportXlsxAsync, ExportKind.Xlsx);

        /// <summary>
        /// JSON 包会把复盘的结构化数据<b>整份</b>带上（状态记录原文、肯定库、目标卡、归档区），
        /// 而不只是这次选中的那几篇日记 —— 按时间 / 标签 / 单篇导出子集时也一样。
        /// <para>
        /// 方向上是「宁多勿少」：少带会踩到 <c>SourceDiaryId</c> 为空的回流行和手动归档，那是真丢数据。
        /// 但用户该知道这个包比自己以为的大，所以在导出成功的提示里补一句。
        ///（一条复盘数据都没有时不会生成 <c>review.json</c>，那这句也就无从谈起了。）
        /// </para>
        /// </summary>
        private string JsonNote => I18n.T("Note: the JSON package also carries all of your review data (state records, affirmations, goals, archive) - not only the diaries you selected.");

        private async Task Export(Func<List<DiaryModel>, Task<string>> func, ExportKind exportKind,
            string? note = null)
        {
            await InternalVisibleChanged(false);

            var writePermission = await PlatformIntegration.TryStorageWritePermission();
            if (!writePermission)
            {
                await AlertService.InfoAsync(I18n.T("Please grant permission for storage writing"));
                return;
            }

            AlertService.StartLoading();
            try
            {
                var diaries = Value.OrderBy(x => x.CreateTime).ToList();
                string path = await func(diaries);
                if (!string.IsNullOrEmpty(path))
                {
                    string fileName = DiaryFileManager.GetExportFileName(exportKind);
                    bool flag = await PlatformIntegration.SaveFileAsync(fileName, path);
                    if (flag)
                    {
                        await AlertService.SuccessAsync(note is null
                            ? I18n.T("Export successfully")
                            : $"{I18n.T("Export successfully")} · {note}");
                        await HandleAchievements(Achievement.Export);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError(new EventId(1, "Error"), e, "Create file wrong");
                await AlertService.ErrorAsync(I18n.T("Export failed"));
            }
            finally
            {
                AlertService.StopLoading();
            }
        }
    }
}
