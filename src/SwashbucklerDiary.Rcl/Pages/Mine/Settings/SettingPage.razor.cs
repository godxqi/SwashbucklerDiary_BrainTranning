using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Rcl.Pages
{
    public partial class SettingPage : ImportantComponentBase
    {
        /// <summary>
        /// 锚定日芯片的值，按「周一 → 周日」的顺序排。
        /// 值本身是 <see cref="DayOfWeek"/>：周日是 0，所以周一在最前、周日在最后。
        /// </summary>
        private static readonly int[] anchorDayValues = [1, 2, 3, 4, 5, 6, 0];

        private static readonly string[] anchorDayKeys =
        [
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday",
            "Sunday",
        ];

        private bool showClearCache;

        private bool showStatisticsCard;

        private bool showQuickRecord;

        private string? cacheSize;

        private int weeklyAnchorDay;

        private int weeklyGraceDays;

        private int monthlyGraceDays;

        [Inject]
        private IAppFileSystem AppFileSystem { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            await UpdateCacheSize();
        }

        protected override void ReadSettings()
        {
            base.ReadSettings();

            showStatisticsCard = SettingService.Get(s => s.StatisticsCard);
            showQuickRecord = SettingService.Get(s => s.QuickRecord);

            //需求 14 的三项。复盘主页上的节拍判定每次都现读这几项，所以改完回复盘主页即生效。
            weeklyAnchorDay = SettingService.Get(s => s.WeeklyReviewAnchorDay, (int)DayOfWeek.Friday);
            weeklyGraceDays = SettingService.Get(s => s.WeeklyReviewGraceDays, 2);
            monthlyGraceDays = SettingService.Get(s => s.MonthlyReviewGraceDays, 3);
        }

        private async Task SetAnchorDayAsync(int value)
        {
            weeklyAnchorDay = value;
            await SettingService.SetAsync(s => s.WeeklyReviewAnchorDay, value);
        }

        private async Task SetWeeklyGraceAsync(int value)
        {
            weeklyGraceDays = value;
            await SettingService.SetAsync(s => s.WeeklyReviewGraceDays, value);
        }

        private async Task SetMonthlyGraceAsync(int value)
        {
            monthlyGraceDays = value;
            await SettingService.SetAsync(s => s.MonthlyReviewGraceDays, value);
        }

        private async Task UpdateCacheSize()
        {
            cacheSize = await AppFileSystem.GetCacheSizeAsync();
        }

        private async Task ClearCache()
        {
            showClearCache = false;
            await AppFileSystem.ClearCacheAsync();
            await UpdateCacheSize();
            await AlertService.SuccessAsync(I18n.T("Clean up successfully"));
        }
    }
}
