using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class Setting
    {
        public string Language { get; set; } = "zh-CN";
        public bool Title { get; set; }
        public bool Markdown { get; set; } = true;
        public string NickName { get; set; } = string.Empty;
        public string Sign { get; set; } = string.Empty;
        public string Avatar { get; set; } = "_content/SwashbucklerDiary.Rcl/logo/logo.jpg";
        public bool HidePrivacyModeEntrance { get; set; }
        public string PrivacyModeEntrancePassword { get; set; } = string.Empty;
        public string PrivacyModeFunctionSearchKey { get; set; } = string.Empty;
        public bool PrivacyModeDark { get; set; } = true;
        public bool SetPrivacyDiary { get; set; } = true;
        public int Theme { get; set; } = 0;
        public bool FirstSetLanguage { get; set; }
        public bool FirstAgree { get; set; }
        public string WebDavConfig { get; set; } = string.Empty;
        public bool WelcomeText { get; set; }
        public bool IndexDate { get; set; }
        public bool DiaryCardIcon { get; set; } = true;
        public int AlertTimeout { get; set; } = 2000;
        public bool WebDAVCopyResources { get; set; }
        public string DiaryCardTimeFormat { get; set; } = "MM/dd";
        public bool AchievementsAlert { get; set; } = true;
        public string LANDeviceName { get; set; } = string.Empty;
        public int LANScanPort { get; set; } = 5299;
        public int LANTransmissionPort { get; set; } = 52099;
        public bool StatisticsCard { get; set; } = true;
        public int EditAutoSave { get; set; } = 5;
        public string DiarySort { get; set; } = string.Empty;
        public string TagSort { get; set; } = string.Empty;
        public string LocationSort { get; set; } = string.Empty;
        public bool UpdatePrompt { get; set; } = true;
        public int UpdatePromptIntervalDay { get; set; } = 1;
        public int UpdateMethod { get; set; } = 0;
        public DateTime LastUpdatePromptTime { get; set; } = DateTime.MinValue;
        public bool ImageLazy { get; set; } = true;
        public bool FirstLineIndent { get; set; }
        public bool CodeLineNumber { get; set; }
        public bool TaskListLineThrough { get; set; }
        public bool DiaryCardTags { get; set; }
        public bool DiaryIconText { get; set; }
        public bool OtherInfo { get; set; } = true;
        public bool AutoPlay { get; set; }
        public bool Outline { get; set; }
        public bool RigthOutline { get; set; } = true;
        public bool DiaryCardLocation { get; set; }
        public string UrlScheme { get; set; } = SchemeConstants.SwashbucklerDiary;
        public int FirstDayOfWeek { get; set; } = 0;
        public int UseTemplateMethod { get; set; } = 0;
        public bool SelectTemplateWhenCreate { get; set; }
        public string DiaryTimeFormat { get; set; } = "yyyy/MM/dd dddd";
        public bool QuickRecord { get; set; }
        public string DefaultTemplateId { get; set; } = string.Empty;
        public bool DiaryCardMarkdown { get; set; }
        public string DiaryInsertTimeFormat { get; set; } = "yyyy/MM/dd HH:mm:ss";
        public string AppLockNumberPassword { get; set; } = string.Empty;
        public bool AppLockBiometric { get; set; }
        public bool LockAppWhenLeave { get; set; }
        public string AppLockPatternPassword { get; set; } = string.Empty;
        public string PrivacyModeEntrancePasswordSalt { get; set; } = string.Empty;
        public string AppLockNumberPasswordSalt { get; set; } = string.Empty;
        public string AppLockPatternPasswordSalt { get; set; } = string.Empty;
        public bool OriginalFileName { get; set; }
        public bool LinkCard { get; set; } = true;
        public bool DiaryCardDefaultTitle { get; set; } = true;

        // ---- 复盘节拍（方法文档 2 / 3、需求 14）----
        // 周课锚定周五、宽容到周日；月课锚定月末、宽容到次月 3 号。
        // 用 DayOfWeek 的数值，5 = Friday。
        public int WeeklyReviewAnchorDay { get; set; } = 5;
        public int WeeklyReviewGraceDays { get; set; } = 2;
        public int MonthlyReviewGraceDays { get; set; } = 3;

        // ---- 复盘主页 ----
        public int KanbanDefaultTab { get; set; }
        public bool AffirmationBarVisible { get; set; } = true;
        public int AffirmationBarCount { get; set; } = 3;

        // 注意：复盘的三套「模板」最后没有走模板系统。
        // 日课/周课/月课都做成了有固定章节的结构化表单，骨架由代码 + i18n 定义
        // （见 ReviewSectionText），比让用户维护一份 Markdown 骨架更省事，也不会出现
        // 「文案改了、库里那份模板还是旧的」这种过期问题。
    }
}
