using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    /// <summary>
    /// 三个优点表（方法文档 6）。这一组表的用法只有一个：<b>在状态糟糕的时候被调用</b>
    /// （文档 6 开头就是这么写的）——「这三个表就是为这一刻建的，不在这里调用，它们就只是收藏」。
    /// <para>
    /// 三个表共用一个页面，靠 <c>?kind=</c> 区分（积极 / 感恩 / 成就），页内也能切。
    /// 成就列表顶部多一行「增长」：文档 6.3 要的是<b>观察它长出来的样子</b>，
    /// 据此决定该加强、删除还是添加。
    /// </para>
    /// </summary>
    public partial class VirtuePage : ImportantComponentBase
    {
        private static readonly VirtueKind[] kinds = Enum.GetValues<VirtueKind>();

        private VirtueKind kind = VirtueKind.Positive;

        private List<VirtueEntryModel> entries = [];

        private List<AchievementGrowth> growth = [];

        private bool showEdit;

        private bool editing;

        private VirtueEntryModel? editingEntry;

        private bool showDelete;

        private VirtueEntryModel? pendingDelete;

        private bool showDatePicker;

        /// <summary>
        /// 新记的几条算在哪一天。默认今天；补过去的优点表时由上面的日期按钮改。
        /// 每次<b>重新进入</b>这一页都会回到今天——不然补完一批忘了改回来，
        /// 下次随手记的一条会悄悄落到上个月的日期里。
        /// </summary>
        private DateTime newEntryDate = DateTime.Today;

        /// <summary>日历的可选上界：补的是过去，写不了未来。</summary>
        private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

        private DateOnly SelectedDate
        {
            get => DateOnly.FromDateTime(newEntryDate);
            set
            {
                if (value == default)
                {
                    return;
                }

                newEntryDate = value.ToDateTime(TimeOnly.MinValue);
            }
        }

        [Inject]
        private IVirtueService VirtueService { get; set; } = default!;

        /// <summary>从「库表」进来时指定看哪一栏。缺省是积极列表。</summary>
        [SupplyParameterFromQuery]
        private int? Kind { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (Kind is int value && Enum.IsDefined((VirtueKind)value))
            {
                kind = (VirtueKind)value;
            }

            await LoadAsync();

            await base.OnInitializedAsync();
        }

        protected override async Task OnResume()
        {
            //离开再回来时把「记到哪天」收回今天，免得上次补足留下的日期被顺手继承
            newEntryDate = DateTime.Today;

            await LoadAsync();

            await base.OnResume();
        }

        private async Task LoadAsync()
        {
            entries = await VirtueService.QueryAsync(kind);

            //增长只在成就那一栏有意义；顺手把总数也取回来，免得页面上再算一遍
            growth = kind == VirtueKind.Achievement
                ? await VirtueService.GetAchievementGrowthAsync()
                : [];

            await InvokeAsync(StateHasChanged);
        }

        private async Task SwitchKindAsync(VirtueKind target)
        {
            if (kind == target)
            {
                return;
            }

            kind = target;

            //把栏位写回地址栏：不然切到「成就列表」之后离开再进，会跳回积极列表
            To($"virtue?kind={(int)kind}", replace: true);

            await LoadAsync();
        }

        private void OpenAdd()
        {
            editing = false;
            editingEntry = null;
            showEdit = true;
        }

        private void OpenEdit(VirtueEntryModel entry)
        {
            editing = true;
            editingEntry = entry;
            showEdit = true;
        }

        private void NewEntryDateChanged(DateOnly date)
        {
            SelectedDate = date;
        }

        private async Task SaveAsync(string content)
        {
            showEdit = false;

            string value = content?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return;
            }

            if (editingEntry is null)
            {
                if (!await VirtueService.AddAsync(kind, value, newEntryDate))
                {
                    await AlertService.ErrorAsync(I18n.T("Add failed"));
                    return;
                }
            }
            else
            {
                editingEntry.Content = value;
                if (!await VirtueService.UpdateAsync(editingEntry))
                {
                    await AlertService.ErrorAsync(I18n.T("Change failed"));
                    return;
                }
            }

            await LoadAsync();
        }

        private void AskDelete(VirtueEntryModel entry)
        {
            pendingDelete = entry;
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

            if (!await VirtueService.DeleteAsync(target.Id))
            {
                await AlertService.ErrorAsync(I18n.T("Delete failed"));
                return;
            }

            entries.RemoveAll(it => it.Id == target.Id);

            //成就那一栏删掉一条，顶部的增长数字也得跟着变
            if (kind == VirtueKind.Achievement)
            {
                growth = await VirtueService.GetAchievementGrowthAsync();
            }

            await InvokeAsync(StateHasChanged);
        }

        // ———————————————— 展示 ————————————————

        private string KindName(VirtueKind value) => I18n.T(value switch
        {
            VirtueKind.Gratitude => "Gratitude list",
            VirtueKind.Achievement => "Achievement list",
            _ => "Positive list",
        });

        /// <summary>每张表的开场白，取自文档 6 对应小节。</summary>
        private string KindDescription => I18n.T(kind switch
        {
            VirtueKind.Gratitude => "Write down what you are grateful for. Stop and actually feel it, one by one, instead of skimming past in your head.",
            VirtueKind.Achievement => "Everything you have ever longed for: roles, abilities, resources - plus the achievements that prove your stamina, skill and determination.",
            _ => "The friends and family who make you feel good (and a plan to see them), plus the things you appreciate about yourself.",
        });

        private string GrowthSummary
        {
            get
            {
                if (kind != VirtueKind.Achievement)
                {
                    return string.Empty;
                }

                var (year, month) = (DateTime.Now.Year, DateTime.Now.Month);
                int thisMonth = growth
                    .FirstOrDefault(it => it.Year == year && it.Month == month)?.Count ?? 0;

                return thisMonth == 0
                    ? $"{I18n.T("Achievement growth")} · {growth.Sum(it => it.Count)}"
                    : $"{I18n.T("Achievement growth")} · {growth.Sum(it => it.Count)} · {year:0000}/{month:00} +{thisMonth}";
            }
        }
    }
}
