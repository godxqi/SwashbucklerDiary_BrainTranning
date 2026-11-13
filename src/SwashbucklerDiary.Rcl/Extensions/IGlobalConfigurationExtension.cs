using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Rcl.Extensions
{
    public static class IGlobalConfigurationExtension
    {
        private readonly static string errorIcon = "close";

        public static string GetWeatherIcon(this IGlobalConfiguration globalConfiguration, string key)
        {
            if (string.IsNullOrEmpty(key) || !globalConfiguration.WeatherIcons.TryGetValue(key, out string? value))
            {
                return errorIcon;
            }

            return value;
        }

        public static string GetMoodIcon(this IGlobalConfiguration globalConfiguration, string key)
        {
            if (string.IsNullOrEmpty(key) || !globalConfiguration.MoodIcons.TryGetValue(key, out string? value))
            {
                return errorIcon;
            }

            return value;
        }

        /// <summary>八大情绪的图标。key 传 <see cref="Shared.MoodKind"/> 的名字。</summary>
        public static string GetEmotionIcon(this IGlobalConfiguration globalConfiguration, Shared.MoodKind mood)
        {
            return GetEmotionIcon(globalConfiguration, mood.ToString());
        }

        private static string GetEmotionIcon(IGlobalConfiguration globalConfiguration, string key)
        {
            if (string.IsNullOrEmpty(key) || !globalConfiguration.EmotionIcons.TryGetValue(key, out string? value))
            {
                return errorIcon;
            }

            return value;
        }

        /// <summary>课题类型的图标。key 传 <see cref="Shared.LessonType"/> 的名字。</summary>
        public static string GetLessonTypeIcon(this IGlobalConfiguration globalConfiguration, Shared.LessonType type)
        {
            return GetLessonTypeIcon(globalConfiguration, type.ToString());
        }

        private static string GetLessonTypeIcon(IGlobalConfiguration globalConfiguration, string key)
        {
            if (string.IsNullOrEmpty(key) || !globalConfiguration.LessonTypeIcons.TryGetValue(key, out string? value))
            {
                return errorIcon;
            }

            return value;
        }

        public static string GetPlatformIcon(this IGlobalConfiguration globalConfiguration, AppOperatingSystem appPlatform)
        {
            if (globalConfiguration.AppPlatformIcons.TryGetValue(appPlatform, out string? value))
            {
                return value;
            }

            if (globalConfiguration.AppPlatformIcons.TryGetValue(AppOperatingSystem.Unknown, out string? value2))
            {
                return value2;
            }

            return errorIcon;
        }
    }
}
