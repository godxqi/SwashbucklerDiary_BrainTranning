using Masa.Blazor;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Extensions;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Rcl.Web.Essentials;
using SwashbucklerDiary.WebAssembly.Essentials;

namespace SwashbucklerDiary.WebAssembly.Extensions
{
    public static partial class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDependencyInjection(this IServiceCollection services)
        {
            services.AddSingleton<IProxyService, Services.ProxyService>();
            services.AddSingleton<IStaticWebAssets, Essentials.StaticWebAssets>();
            services.AddSingleton<IPreferences, Essentials.Preferences>();
            services.AddSingleton<ISettingService, Services.SettingService>();
            services.AddSingleton<RouteMatcher>(_ => new RouteMatcher(App.Assemblies));
            services.AddSingleton<INavigateController, NavigateController>();
            services.AddSingleton<IGlobalConfiguration, GlobalConfiguration>();
            services.AddSingleton<IAlertService, AlertService>();
            services.AddSingleton<II18nService>(sp =>
            {
                return (I18nService)sp.GetRequiredService<I18n>();
            });
            services.AddSingleton<IVersionTracking, VersionTracking>();
            services.AddSingleton<Essentials.PlatformIntegrationJSModule>();
            services.AddSingleton<IPlatformIntegration, Essentials.PlatformIntegration>();
            services.AddSingleton<IAppFileSystem, Essentials.AppFileSystem>();
            services.AddSingleton<IMediaResourceManager, Rcl.Web.Services.MediaResourceManager>();
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

            services.AddSingleton<AppLifecycleJSModule>();
            services.AddSingleton<IAppLifecycle, Rcl.Web.Essentials.AppLifecycle>();
            services.AddSingleton<SystemThemeJSModule>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<IAccessExternal, Rcl.Web.Services.AccessExternal>();

            services.AddSingleton<IDiaryFileManager, Services.DiaryFileManager>();
            services.AddSingleton<IAvatarService, Services.AvatarService>();
            services.AddScoped<IScreenshot, Screenshot>();
            services.AddSingleton<IVersionUpdataManager, Services.VersionUpdataManager>();

            return services;
        }
    }
}
