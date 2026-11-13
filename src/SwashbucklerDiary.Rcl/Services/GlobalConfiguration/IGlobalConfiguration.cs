using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public interface IGlobalConfiguration
    {
        Dictionary<string, string> WeatherIcons { get; set; }

        Dictionary<string, string> MoodIcons { get; set; }

        /// <summary>八大情绪（方法文档 1.6）的图标。key 是 MoodKind 的名字。</summary>
        Dictionary<string, string> EmotionIcons { get; set; }

        /// <summary>课题类型的图标。key 是 LessonType 的名字。</summary>
        Dictionary<string, string> LessonTypeIcons { get; set; }

        Dictionary<AppOperatingSystem, string> AppPlatformIcons { get; set; }

        List<AchievementModel> Achievements { get; set; }

        Task InitializeAsync();
    }
}
