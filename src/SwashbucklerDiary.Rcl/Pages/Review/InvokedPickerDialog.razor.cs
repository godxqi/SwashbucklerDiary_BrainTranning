using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>「现在要调用哪一条」选中的那一条：指向哪张表 + 哪一行 + 显示成什么文字。</summary>
    public sealed record InvokedReference(InvokedKind Kind, Guid Id, string Text);

    /// <summary>选择列表里的一项。三种来源（优点表 / 肯定库 / 宏观目标）在这里统一成同一个形状。</summary>
    internal sealed record InvokedPickerItem(Guid Id, string Text);

    /// <summary>
    /// 状态管理表 5.1 里「现在要调用哪一条」的选择器。
    /// <para>
    /// 文档把这一格标成<b>全表最重要的一格</b>：三个优点表就是为这一刻建的，
    /// 不在这里调用，它们就只是收藏。所以这里把五个库并排列出来让人现翻，
    /// 而不是让人凭记忆打字。
    /// </para>
    /// </summary>
    public partial class InvokedPickerDialog : DialogComponentBase
    {
        /// <summary>五栏。三个优点表按文档 5.1 的原话分成「成就 / 感恩 / 积极」三栏各一个入口。</summary>
        private static readonly string[] sourceKeys =
        [
            "Positive list",
            "Gratitude list",
            "Achievement list",
            "Affirmation library",
            "Macro goal",
        ];

        private int sourceIndex;

        private List<InvokedPickerItem> positives = [];

        private List<InvokedPickerItem> gratitudes = [];

        private List<InvokedPickerItem> achievements = [];

        private List<InvokedPickerItem> affirmations = [];

        private List<InvokedPickerItem> macroGoals = [];

        [Inject]
        private IVirtueService VirtueService { get; set; } = default!;

        [Inject]
        private IAffirmationService AffirmationService { get; set; } = default!;

        [Inject]
        private IMacroGoalService MacroGoalService { get; set; } = default!;

        [Parameter]
        public InvokedKind? Kind { get; set; }

        [Parameter]
        public Guid? Id { get; set; }

        [Parameter]
        public EventCallback<InvokedReference> OnOK { get; set; }

        /// <summary>点了「清除」——原来调用过一条，现在不调用了。</summary>
        [Parameter]
        public EventCallback OnClear { get; set; }

        private bool HasSelection => Kind is not null && Id is not null;

        private Guid? SelectedId => Id;

        private List<InvokedPickerItem> CurrentItems => sourceIndex switch
        {
            0 => positives,
            1 => gratitudes,
            2 => achievements,
            3 => affirmations,
            _ => macroGoals,
        };

        private int CurrentCount => CurrentItems.Count;

        private async void HandleOnBeforeShowContent()
        {
            //打开时默认落在「当时选中的那一栏」，没有就落在积极列表
            sourceIndex = Kind switch
            {
                InvokedKind.Virtue => ResolveVirtueSourceIndex(),
                InvokedKind.Affirmation => 3,
                InvokedKind.MacroGoal => 4,
                _ => 0,
            };

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            positives = ToItems(await VirtueService.QueryAsync(VirtueKind.Positive));
            gratitudes = ToItems(await VirtueService.QueryAsync(VirtueKind.Gratitude));
            achievements = ToItems(await VirtueService.QueryAsync(VirtueKind.Achievement));

            affirmations = [.. (await AffirmationService.GetActiveAsync())
                .Where(it => !string.IsNullOrWhiteSpace(it.Content))
                .Select(it => new InvokedPickerItem(it.Id, it.Content!))];

            macroGoals = [.. (await MacroGoalService.GetActiveAsync())
                .Where(it => !string.IsNullOrWhiteSpace(it.Title))
                .Select(it => new InvokedPickerItem(it.Id, it.Title!))];

            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// 优点表三栏共用一个 <see cref="InvokedKind.Virtue"/>，所以要从选中行的 Id 反查它属于哪一栏。
        /// </summary>
        private int ResolveVirtueSourceIndex()
        {
            if (Id is not Guid id)
            {
                return 0;
            }

            if (gratitudes.Any(it => it.Id == id))
            {
                return 1;
            }

            return achievements.Any(it => it.Id == id) ? 2 : 0;
        }

        private static List<InvokedPickerItem> ToItems(List<VirtueEntryModel> entries)
        {
            return [.. entries
                .Where(it => !string.IsNullOrWhiteSpace(it.Content))
                .Select(it => new InvokedPickerItem(it.Id, it.Content!))];
        }

        private void SelectSource(int index)
        {
            sourceIndex = index;
        }

        private async Task Choose(InvokedPickerItem item)
        {
            var kind = sourceIndex switch
            {
                0 or 1 or 2 => InvokedKind.Virtue,
                3 => InvokedKind.Affirmation,
                _ => InvokedKind.MacroGoal,
            };

            if (OnOK.HasDelegate)
            {
                await OnOK.InvokeAsync(new InvokedReference(kind, item.Id, item.Text));
            }
        }

        private async Task HandleOnClear()
        {
            if (OnClear.HasDelegate)
            {
                await OnClear.InvokeAsync();
            }
        }
    }
}
