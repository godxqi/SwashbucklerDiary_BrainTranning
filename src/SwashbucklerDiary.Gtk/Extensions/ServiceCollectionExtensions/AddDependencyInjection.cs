using Masa.Blazor;
using Microsoft.Extensions.DependencyInjection;
using SwashbucklerDiary.Gtk.Essentials;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Extensions;
using SwashbucklerDiary.Rcl.Hybird.Services;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Gtk.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
        {
            services.AddSingleton<GtkSystemThemeManager>();

            services.AddSingleton<IProxyService, Rcl.Hybird.Services.ProxyService>();
            services.AddSingleton<IStaticWebAssets, Essentials.StaticWebAssets>();
            services.AddSingleton<IPreferences, Essentials.Preferences>();
            services.AddSingleton<ISettingService, Services.SettingService>();
            services.AddSingleton<RouteMatcher>(_ => new RouteMatcher(Routes.Assemblies));
            services.AddSingleton<INavigateController, NavigateController>();
            services.AddSingleton<IGlobalConfiguration, GlobalConfiguration>();
            services.AddSingleton<IAlertService, AlertService>();
            services.AddSingleton<II18nService>(sp =>
            {
                return (I18nService)sp.GetRequiredService<I18n>();
            });
            services.AddSingleton<Rcl.Essentials.IVersionTracking, Essentials.VersionTracking>();
            services.AddSingleton<IPlatformIntegration, PlatformIntegration>();
            services.AddSingleton<IAppFileSystem, Essentials.AppFileSystem>();
            services.AddSingleton<IMediaResourceManager, Services.MediaResourceManager>();
            services.AddSingleton<BreakpointService>();
            services.AddSingleton<IAppLockService, AppLockService>();

            services.AddRclDependencyInjection();

            services.AddSingleton<IDiaryRepository, DiaryRepository>();
            services.AddSingleton<ITagRepository, TagRepository>();
            services.AddSingleton<IUserAchievementRepository, UserAchievementRepository>();
            services.AddSingleton<IUserStateModelRepository, UserStateModelRepository>();
            services.AddSingleton<ILocationRepository, LocationRepository>();
            services.AddSingleton<ILogRepository, LogRepository>();
            services.AddSingleton<IResourceRepository, ResourceRepository>();
            // 复盘系统的 12 张表彼此同构，一个开放泛型注册就够
            services.AddSingleton(typeof(IReviewRepository<>), typeof(ReviewRepository<>));

            services.AddSingleton<ITagService, TagService>();
            services.AddSingleton<IDiaryService, DiaryService>();
            services.AddSingleton<IAchievementService, AchievementService>();
            services.AddSingleton<ILocationService, LocationService>();
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<IResourceService, ResourceService>();
            // 复盘系统
            services.AddSingleton<IReviewScheduleService, ReviewScheduleService>();
            services.AddSingleton<IDailyReviewService, DailyReviewService>();
            services.AddSingleton<IAffirmationService, AffirmationService>();
            services.AddSingleton<IStumblingBlockService, StumblingBlockService>();
            services.AddSingleton<IWeeklyReviewService, WeeklyReviewService>();
            services.AddSingleton<IArchiveService, ArchiveService>();
            services.AddSingleton<IVirtueService, VirtueService>();
            services.AddSingleton<ITriggerService, TriggerService>();
            services.AddSingleton<IMacroGoalService, MacroGoalService>();
            services.AddSingleton<IMonthlyReviewService, MonthlyReviewService>();
            services.AddSingleton<IStateRecordService, StateRecordService>();
            services.AddSingleton<ILessonService, LessonService>();
            services.AddSingleton<IDailyEventService, DailyEventService>();
            services.AddSingleton<IReviewStatsService, ReviewStatsService>();
            services.AddSingleton<IReviewExportService, ReviewExportService>();

            services.AddSingleton<IAppLifecycle>(sp => Essentials.AppLifecycle.Default);
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IAccessExternal, Services.AccessExternal>();
            services.AddSingleton<IDiaryFileManager, Services.DiaryFileManager>();
            services.AddSingleton<IAvatarService, Services.AvatarService>();
            services.AddScoped<IScreenshot, Screenshot>();
            services.AddSingleton<IVersionUpdataManager, Services.VersionUpdataManager>();
            services.AddSingleton<IWebDAV, WebDAV>();
            services.AddSingleton<ILANSenderService, LANSenderService>();
            services.AddSingleton<ILANReceiverService, LANReceiverService>();
            services.AddSingleton<IProxyService, ProxyService>();

            return services;
        }
    }
}
