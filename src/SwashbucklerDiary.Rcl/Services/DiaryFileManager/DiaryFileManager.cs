using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Shared;
using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SwashbucklerDiary.Rcl.Services
{
    public abstract class DiaryFileManager : IDiaryFileManager
    {
        protected readonly IAppFileSystem _appFileSystem;

        protected readonly IPlatformIntegration _platformIntegration;

        protected readonly II18nService _i18n;

        protected readonly IMediaResourceManager _mediaResourceManager;

        protected readonly IDiaryService _diaryService;

        protected readonly IResourceService _resourceService;

        protected readonly ISettingService _settingService;

        protected readonly IAvatarService _avatarService;

        protected readonly IReviewExportService _reviewExportService;

        protected const string exportFileNamePrefix = "SwashbucklerDiaryExport";

        protected const string backupFileNamePrefix = "SwashbucklerDiaryBackup";

        protected const string versionInfoFileName = "version.json";

        /// <summary>复盘结构化数据的可读导出。导入时要把它从「一篇日记一个 json」的循环里排除掉。</summary>
        protected const string reviewInfoFileName = "review.json";

        protected const string settingsFileName = "settings.json";

        protected const string exportFileNameDateTimeFormat = "yyyyMMddHHmmss";

        protected JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        protected string DatabaseFilename => SQLiteConstants.MainDatabaseFilename;

        protected abstract string DatabasePath { get; }

        protected abstract string PrivacyDatabasePath { get; }

        protected static readonly List<string> excludedSettings =
        [
            nameof(Setting.WebDavConfig),
            nameof(Setting.PrivacyModeEntrancePassword),
            nameof(Setting.PrivacyModeDark),
            nameof(Setting.PrivacyModeFunctionSearchKey),
            nameof(Setting.HidePrivacyModeEntrance),
            nameof(Setting.SetPrivacyDiary),
            nameof(Setting.AppLockNumberPassword),
            nameof(Setting.AppLockPatternPassword),
            nameof(Setting.AppLockBiometric),
            nameof(Setting.LockAppWhenLeave),
            nameof(Setting.PrivacyModeEntrancePasswordSalt),
            nameof(Setting.AppLockNumberPasswordSalt),
            nameof(Setting.AppLockPatternPasswordSalt),
        ];

        public DiaryFileManager(IAppFileSystem appFileSystem,
            IPlatformIntegration platformIntegration,
            II18nService i18nService,
            IMediaResourceManager mediaResourceManager,
            IDiaryService diaryService,
            IResourceService resourceService,
            ISettingService settingService,
            IAvatarService avatarService,
            IReviewExportService reviewExportService)
        {
            _appFileSystem = appFileSystem;
            _platformIntegration = platformIntegration;
            _i18n = i18nService;
            _mediaResourceManager = mediaResourceManager;
            _diaryService = diaryService;
            _resourceService = resourceService;
            _settingService = settingService;
            _avatarService = avatarService;
            _reviewExportService = reviewExportService;
        }

        public async Task<string> ExportDBAsync(bool copyResources)
        {
            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, "DB");
            string zipFilePath = Path.Combine(_appFileSystem.CacheDirectory, $"{backupFileNamePrefix}.zip");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            string databasePath = GetCurrentDatabasePath();
            var destFileName = Path.Combine(outputFolder, DatabaseFilename);
            SqliteConnection.ClearAllPools();
            await _appFileSystem.CopyFileAsync(databasePath, destFileName).ConfigureAwait(false);
            if (copyResources)
            {
                await CopyDiaryResourceAsync(outputFolder).ConfigureAwait(false);
                await CopyAvatarAsync(outputFolder).ConfigureAwait(false);
            }

            await CreateSettingsFileAsync(outputFolder).ConfigureAwait(false);
            await CreateExportVersionInfoAsync(outputFolder, ".db3").ConfigureAwait(false);

            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            await Task.Run(() => ZipFile.CreateFromDirectory(outputFolder, zipFilePath)).ConfigureAwait(false);
            return zipFilePath;
        }

        public async Task<string> ExportJsonAsync(List<DiaryModel> diaries)
        {
            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, "Json");
            string zipFilePath = Path.Combine(_appFileSystem.CacheDirectory, $"{exportFileNamePrefix}Json.zip");

            string fileSuffix = ".json";

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            foreach (var item in diaries)
            {
                string fileName = item.CreateTime.ToString("yyyy-MM-dd") + fileSuffix;
                string filePath = Path.Combine(outputFolder, fileName);

                string content = JsonSerializer.Serialize(item, jsonSerializerOptions);
                await WriteToFileAsync(filePath, content).ConfigureAwait(false);
            }

            await CopyDiaryResourceAsync(diaries, outputFolder).ConfigureAwait(false);

            await WriteReviewInfoAsync(outputFolder).ConfigureAwait(false);

            await CreateExportVersionInfoAsync(outputFolder, fileSuffix).ConfigureAwait(false);

            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            // 将所有json文件添加到压缩包中
            await Task.Run(() => ZipFile.CreateFromDirectory(outputFolder, zipFilePath)).ConfigureAwait(false);
            return zipFilePath;
        }

        public Task<string> ExportMdAsync(List<DiaryModel> diaries)
            => ExportTextFileAsync(diaries, "Markdown", ".md");

        public Task<string> ExportTxtAsync(List<DiaryModel> diaries)
            => ExportTextFileAsync(diaries, "Txt", ".txt");

        private async Task<string> ExportTextFileAsync(List<DiaryModel> diaries, string name, string fileExtension)
        {
            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, name);
            string zipFilePath = Path.Combine(_appFileSystem.CacheDirectory, $"{exportFileNamePrefix}{name}.zip");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            foreach (var item in diaries)
            {
                string fileName = item.CreateTime.ToString(exportFileNameDateTimeFormat) + fileExtension;
                string filePath = Path.Combine(outputFolder, fileName);
                await WriteToFileAsync(filePath, item.Content).ConfigureAwait(false);
            }

            await CopyDiaryResourceAsync(diaries, outputFolder).ConfigureAwait(false);

            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            await Task.Run(() => ZipFile.CreateFromDirectory(outputFolder, zipFilePath)).ConfigureAwait(false);
            return zipFilePath;
        }

        public Task<string> ExportXlsxAsync(List<DiaryModel> diaries)
        {
            string filePath = Path.Combine(_appFileSystem.CacheDirectory, $"{exportFileNamePrefix}Xlsx.xlsx");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            var timeCol = _i18n.T("Time");
            var weatherCol = _i18n.T("Weather");
            var moodCol = _i18n.T("Mood");
            var locationCol = _i18n.T("Location");
            var tagCol = _i18n.T("Tag");
            var titleCol = _i18n.T("Title");
            var contentCol = _i18n.T("Content");

            return Task.Run(() =>
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Sheet1");

                // 添加表头
                worksheet.Cell(1, 1).Value = timeCol;
                worksheet.Cell(1, 2).Value = weatherCol;
                worksheet.Cell(1, 3).Value = moodCol;
                worksheet.Cell(1, 4).Value = locationCol;
                worksheet.Cell(1, 5).Value = tagCol;
                worksheet.Cell(1, 6).Value = titleCol;
                worksheet.Cell(1, 7).Value = contentCol;

                // 处理数据行
                for (int i = 0; i < diaries.Count; i++)
                {
                    var item = diaries[i];
                    int row = i + 2;

                    worksheet.Cell(row, 1).Value = item.CreateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    worksheet.Cell(row, 2).Value = item.Weather is null ? string.Empty : _i18n.T(item.Weather);
                    worksheet.Cell(row, 3).Value = item.Mood is null ? string.Empty : _i18n.T(item.Mood);
                    worksheet.Cell(row, 4).Value = item.Location ?? string.Empty;

                    worksheet.Cell(row, 5).Value = item.Tags is not null && item.Tags.Count > 0
                        ? string.Join(", ", item.Tags.Select(it => it.Name))
                        : string.Empty;

                    worksheet.Cell(row, 6).Value = item.Title ?? string.Empty;
                    worksheet.Cell(row, 7).Value = item.Content ?? string.Empty;
                }

                workbook.SaveAs(filePath);
                return filePath;
            });
        }

        protected void CopyUriFileToOutFolder(string uri, string outFolder)
        {
            var filePath = _mediaResourceManager.RelativeUrlToFilePath(uri);
            if (!File.Exists(filePath))
            {
                return;
            }

            var outFilePath = Path.Combine(outFolder, uri.Replace('/', Path.DirectorySeparatorChar)); ;
            var outFileDir = Path.GetDirectoryName(outFilePath);
            if (outFileDir is null)
            {
                return;
            }

            if (!Directory.Exists(outFileDir))
            {
                Directory.CreateDirectory(outFileDir);
            }

            _appFileSystem.FileCopy(filePath, outFilePath);
        }

        private static async Task WriteToFileAsync(string fileName, string? content)
        {
            int suffix = 1;
            string newFileName = fileName;
            string fileDirectoryName = Path.GetDirectoryName(fileName) ?? "";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            string pathWithoutExtension = Path.Combine(fileDirectoryName, fileNameWithoutExtension);
            string fileExtension = Path.GetExtension(fileName);

            while (File.Exists(newFileName))
            {
                suffix++;
                newFileName = $"{pathWithoutExtension}({suffix}){fileExtension}";
            }

            using StreamWriter writer = new(newFileName);
            await writer.WriteAsync(content).ConfigureAwait(false);
        }

        private async Task CreateExportVersionInfoAsync(string outputFolder, string fileSuffix)
        {
            var exportVersionInfo = new ExportVersionInfo()
            {
                Version = _platformIntegration.AppVersionString,
                FileSuffix = fileSuffix,
                Platform = _platformIntegration.CurrentPlatform.ToString(),
                DateTime = DateTime.Now,
            };

            // 将对象序列化为 JSON 字符串
            string jsonString = JsonSerializer.Serialize(exportVersionInfo);

            // 将 JSON 字符串写入文件
            var jsonPath = Path.Combine(outputFolder, versionInfoFileName);
            await File.WriteAllTextAsync(jsonPath, jsonString).ConfigureAwait(false);
        }

        private async Task CreateSettingsFileAsync(string outputFolder)
        {
            var settingsObject = _settingService.SaveSettingsToObject(it => !excludedSettings.Contains(it));
            string jsonString = JsonSerializer.Serialize(settingsObject);
            var settingsFilePath = Path.Combine(outputFolder, settingsFileName);
            await File.WriteAllTextAsync(settingsFilePath, jsonString).ConfigureAwait(false);
        }

        private async Task CopyAvatarAsync(string outputFolder)
        {
            var avatar = _settingService.Get(it => it.Avatar, null);
            if (string.IsNullOrEmpty(avatar))
            {
                return;
            }

            await Task.Run(() =>
            {
                CopyUriFileToOutFolder(avatar, outputFolder);
            }).ConfigureAwait(false);
        }

        private async Task CopyDiaryResourceAsync(List<DiaryModel> diaries, string outputFolder)
        {
            var resources = diaries.SelectMany(a => a.Resources ?? []).Distinct().ToList();
            await CopyDiaryResource(resources, outputFolder).ConfigureAwait(false);
        }

        private async Task CopyDiaryResourceAsync(string outputFolder)
        {
            var resources = await _resourceService.QueryAsync().ConfigureAwait(false);
            await CopyDiaryResource(resources, outputFolder).ConfigureAwait(false);
        }

        protected async Task CopyDiaryResource(List<ResourceModel> resources, string outputFolder)
        {
            await Task.Run(() =>
            {
                foreach (var resource in resources)
                {
                    if (resource.ResourceUri is null)
                    {
                        return;
                    }

                    CopyUriFileToOutFolder(resource.ResourceUri, outputFolder);
                }
            }).ConfigureAwait(false);
        }

        public string GetExportFileName(ExportKind exportKind)
        {
            string prefix = exportFileNamePrefix + exportKind.ToString();
            string suffix = exportKind == ExportKind.Xlsx ? ".xlsx" : ".zip";
            return InternalGetExportFileName(prefix, suffix);
        }

        public string GetBackupFileName()
        {
            return InternalGetExportFileName(backupFileNamePrefix, ".zip");
        }

        private string InternalGetExportFileName(string prefix, string suffix)
        {
            string dataTime = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string version = _platformIntegration.AppVersionString;
            return $"{prefix}_{dataTime}_v{version}{suffix}";
        }

        public async Task<bool> ImportDBAsync(Stream stream)
        {
            string fileName = Guid.NewGuid().ToString("N") + ".zip";
            string path = await _appFileSystem.CreateTempFileAsync(fileName, stream).ConfigureAwait(false);
            var flag = await ImportDBAsync(path).ConfigureAwait(false);
            File.Delete(path);
            await _appFileSystem.SyncFS();
            return flag;
        }

        public async Task<bool> ImportDBAsync(string filePath)
        {
            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, "DB");

            if (!File.Exists(filePath))
            {
                return false;
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            await Task.Run(() => ZipFile.ExtractToDirectory(filePath, outputFolder)).ConfigureAwait(false);

            Version? version = GetVersion(outputFolder);

            // 获取文件夹下的db文件
            string[] dbFiles = Directory.GetFiles(outputFolder, "*.db3");
            if (dbFiles.Length == 0)
            {
                return false;
            }

            SqliteConnection.ClearAllPools();
            await _appFileSystem.CopyFileAsync(dbFiles[0], GetCurrentDatabasePath()).ConfigureAwait(false);
            if (version is null)
            {
                await AllUseNewResourceUriAsync().ConfigureAwait(false);
                await RestoreOldDiaryResourceAsync(outputFolder).ConfigureAwait(false);
            }
            else
            {
                await RestoreDiaryResourceAsync(outputFolder).ConfigureAwait(false);
            }

            string previousAvatarUri = _settingService.Get(s => s.Avatar);
            await ReadSettingsFileAsync(outputFolder).ConfigureAwait(false);
            RestoreAvatar(outputFolder, previousAvatarUri);
            await _appFileSystem.SyncFS();
            return true;
        }

        private async Task ReadSettingsFileAsync(string outputFolder)
        {
            var settingsJsonPath = Path.Combine(outputFolder, settingsFileName);
            if (!File.Exists(settingsJsonPath)) return;

            using FileStream stream = File.OpenRead(settingsJsonPath);
            var settingsObject = JsonSerializer.Deserialize<Setting>(stream);
            if (settingsObject is null) return;

            await _settingService.SetSettingsFromObjectAsync(settingsObject, it => !excludedSettings.Contains(it)).ConfigureAwait(false);
        }

        private static Version? GetVersion(string outputFolder)
        {
            var versionJsonPath = Path.Combine(outputFolder, versionInfoFileName);
            if (File.Exists(versionJsonPath))
            {
                ExportVersionInfo? exportVersionInfo = null;
                using (FileStream stream = File.OpenRead(versionJsonPath))
                {
                    exportVersionInfo = JsonSerializer.Deserialize<ExportVersionInfo>(stream);
                }

                File.Delete(versionJsonPath);
                if (exportVersionInfo?.Version is not null)
                {
                    return new Version(exportVersionInfo.Version);
                }
            }

            return null;
        }

        public async Task<bool> ImportJsonAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, "Json");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            ZipFile.ExtractToDirectory(filePath, outputFolder);
            Version? version = GetVersion(outputFolder);

            // 获取文件夹下的所有json文件
            string[] jsonFiles = Directory.GetFiles(outputFolder, "*.json");
            if (jsonFiles.Length == 0)
            {
                return false;
            }

            var diaries = new List<DiaryModel>();
            foreach (string jsonFile in jsonFiles)
            {
                //review.json 装的是复盘结构化数据，不是一篇日记——漏掉这一句会当场反序列化炸掉。
                //（version.json 不冲突是因为 GetVersion 读的时候顺手把它删了。）
                if (string.Equals(Path.GetFileName(jsonFile), reviewInfoFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using FileStream openStream = File.OpenRead(jsonFile);
                var diary = await JsonSerializer.DeserializeAsync<DiaryModel>(openStream).ConfigureAwait(false);
                if (diary is not null)
                {
                    diaries.Add(diary);
                }
            }

            if (version is null)
            {
                await UseNewResourceUriAsync(diaries).ConfigureAwait(false);
                await RestoreOldDiaryResourceAsync(outputFolder).ConfigureAwait(false);
            }
            else
            {
                await RestoreDiaryResourceAsync(outputFolder).ConfigureAwait(false);
            }

            //空列表不进 ImportAsync：SqlSugar 的 UpdateNav 拿到空列表的行为没验证过，
            //而包里只有 review.json（没有日记）并不是不可能出现的情况。
            bool result = diaries.Count == 0 || await _diaryService.ImportAsync(diaries).ConfigureAwait(false);

            //复盘数据**不看 result，照常导入**（审查提过要不要用 result 闸门，这里有意不采用）：
            //日记导入返回 false 的常见情形之一是「这些日记库里已经有了、什么都没变」——
            //那种情况下把复盘数据拦住，「再恢复一次同一个备份」就会变成复盘数据没恢复，而且**不会报错**。
            //反过来，日记真导入失败时，复盘行引用的日记会在下一次成功导入后补上，是**可自愈**的。
            //两害相权：静默少恢复 > 短暂多几条悬空引用。
            await ImportReviewInfoAsync(outputFolder).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// 把 12 张复盘表写进 <c>review.json</c>。
        /// <para>
        /// 空包不写：一条复盘数据都没有时多一个空文件，只会让人以为导出坏了。
        /// 也不走 <see cref="WriteToFileAsync"/>——那个方法遇到重名会加 (2) 后缀，
        /// 而导入侧是按名字认这个文件的，名字必须固定。
        /// </para>
        /// </summary>
        protected async Task WriteReviewInfoAsync(string outputFolder)
        {
            var review = await _reviewExportService.ExportAsync().ConfigureAwait(false);
            if (review.IsEmpty)
            {
                return;
            }

            string filePath = Path.Combine(outputFolder, reviewInfoFileName);
            string content = JsonSerializer.Serialize(review, jsonSerializerOptions);
            await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
        }

        /// <summary>
        /// 导入 <c>review.json</c>。文件不存在是<b>正常情况</b>——阶段 4 之前的导出包里没有它，
        /// 老包必须照常导入成功，不能因此报错。
        /// </summary>
        protected async Task ImportReviewInfoAsync(string outputFolder)
        {
            string filePath = Path.Combine(outputFolder, reviewInfoFileName);
            if (!File.Exists(filePath))
            {
                return;
            }

            using FileStream openStream = File.OpenRead(filePath);
            var review = await JsonSerializer
                .DeserializeAsync<ReviewExportModel>(openStream)
                .ConfigureAwait(false);

            if (review is null)
            {
                return;
            }

            await _reviewExportService.ImportAsync(review).ConfigureAwait(false);
        }

        public async Task<bool> ImportMdAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, "Markdown");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            await Task.Run(() => ZipFile.ExtractToDirectory(filePath, outputFolder)).ConfigureAwait(false);

            string[] mdFilePaths = Directory.GetFiles(outputFolder, "*.md", SearchOption.AllDirectories);
            if (mdFilePaths.Length == 0)
            {
                return false;
            }

            var diaries = new List<DiaryModel>();
            foreach (string mdFilePath in mdFilePaths)
            {
                var diary = await ConvertToDiaryAsync(mdFilePath).ConfigureAwait(false);

                if (diary is not null)
                {
                    diaries.Add(diary);
                }
            }

            await RestoreDiaryResourceAsync(outputFolder).ConfigureAwait(false);

            return await _diaryService.ImportAsync(diaries).ConfigureAwait(false);
        }

        private async Task<DiaryModel> ConvertToDiaryAsync(string filePath)
        {
            var diary = new DiaryModel();
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string dateTimeString = fileNameWithoutExtension.Split('(')[0];
            diary.CreateTime = diary.UpdateTime = ConvertToDateTime(dateTimeString);
            diary.Content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            diary.Resources = _mediaResourceManager.GetDiaryResources(diary.Content);
            return diary;
        }

        private static DateTime ConvertToDateTime(string dateTimeString)
        {
            if (DateTime.TryParseExact(dateTimeString, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
            {
                return dateTime;
            }

            if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, out DateTime dateTime2))
            {
                return dateTime2;
            }

            return DateTime.Now;
        }

        protected async Task RestoreDiaryResourceAsync(string outputFolder)
        {
            foreach (var item in _mediaResourceManager.MediaResourceDirectoryPaths)
            {
                var sourceDir = Path.Combine(outputFolder, AppFileSystem.AppDataVirtualDirectoryName, item.Key.ToString());
                if (!Directory.Exists(sourceDir))
                {
                    continue;
                }

                var targetDir = item.Value;
                await _appFileSystem.MoveFolderAsync(sourceDir, targetDir, SearchOption.AllDirectories).ConfigureAwait(false);
            }
        }

        protected async Task RestoreOldDiaryResourceAsync(string outputFolder)
        {
            var sourceDir = Path.Combine(outputFolder, "Image");
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            var targetDir = Path.Combine(_appFileSystem.AppDataDirectory, "Image");
            await _appFileSystem.MoveFolderAsync(sourceDir, targetDir, SearchOption.TopDirectoryOnly).ConfigureAwait(false);
        }

        private void RestoreAvatar(string outputFolder, string previousAvatarUri)
        {
            string avatarUri = _settingService.Get(s => s.Avatar);
            string avatarFileName = Path.GetFileName(avatarUri);
            string sourceFilePath = Path.Combine(outputFolder, AppFileSystem.AppDataVirtualDirectoryName, AvatarService.AvatarDirectoryName, avatarFileName);
            if (!File.Exists(sourceFilePath))
            {
                return;
            }

            string targetFilePath = Path.Combine(_avatarService.AvatarDirectoryPath, avatarFileName);
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }

            _appFileSystem.FileMove(sourceFilePath, targetFilePath);
            if (avatarUri != previousAvatarUri)
            {
                string previousAvatarPath = _mediaResourceManager.RelativeUrlToFilePath(previousAvatarUri);
                if (File.Exists(previousAvatarPath))
                {
                    File.Delete(previousAvatarPath);
                }
            }
        }

        private async Task UseNewResourceUriAsync(List<DiaryModel> diaries)
        {
            await Task.Run(() =>
            {
                foreach (var diary in diaries)
                {
                    if (!string.IsNullOrEmpty(diary.Content))
                    {
                        diary.Content = diary.Content.Replace("appdata:///", "appdata/");
                        diary.Resources = _mediaResourceManager.GetDiaryResources(diary.Content);
                    }
                }
            }).ConfigureAwait(false);
        }

        public async Task AllUseNewResourceUriAsync()
        {
            var diaries = await _diaryService.QueryDiariesAsync().ConfigureAwait(false);
            await _resourceService.DeleteAsync(it => it.ResourceUri.StartsWith("appdata:///")).ConfigureAwait(false);
            await UseNewResourceUriAsync(diaries).ConfigureAwait(false);
            await _diaryService.UpdateIncludesAsync(diaries).ConfigureAwait(false);
        }

        private string GetCurrentDatabasePath()
        {
            bool privacyMode = _settingService.GetTemp(it => it.PrivacyMode);
            return privacyMode ? PrivacyDatabasePath : DatabasePath;
        }

        public async Task<string> ExportResourceFileAsync(List<ResourceModel> resources)
        {
            string outputFolder = Path.Combine(_appFileSystem.CacheDirectory, "ResourceFile");
            string zipFilePath = Path.Combine(_appFileSystem.CacheDirectory, $"{exportFileNamePrefix}ResourceFile.zip");

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            else
            {
                await _appFileSystem.ClearFolderAsync(outputFolder).ConfigureAwait(false);
            }

            await CopyDiaryResource(resources, outputFolder).ConfigureAwait(false);

            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }

            await Task.Run(() => ZipFile.CreateFromDirectory(outputFolder, zipFilePath)).ConfigureAwait(false);
            return zipFilePath;
        }

        public async Task UpdateAllDiariesResourcesAsync()
        {
            var diaries = await _diaryService.QueryDiariesAsync().ConfigureAwait(false);
            await Task.Run(() =>
            {
                foreach (var diary in diaries)
                {
                    if (!string.IsNullOrEmpty(diary.Content))
                    {
                        diary.Resources = _mediaResourceManager.GetDiaryResources(diary.Content);
                    }
                }
            }).ConfigureAwait(false);
            await _diaryService.UpdateIncludesAsync(diaries).ConfigureAwait(false);
        }

        public async Task AllUseNewDiaryReferenceLinkAsync()
        {
            string urlscheme = await _settingService.GetAsync("UrlScheme", SchemeConstants.SwashbucklerDiary);
            var diaries = await _diaryService.QueryDiariesAsync().ConfigureAwait(false);
            string pattern = @"\[([^\]\n]*)]\(read/([^)\n]*)\)";
            string replacement = $"[$1]({urlscheme}://read/$2)";
            await Task.Run(() =>
            {
                foreach (var diary in diaries)
                {
                    if (!string.IsNullOrEmpty(diary.Content))
                    {
                        diary.Content = Regex.Replace(diary.Content, pattern, replacement);
                    }
                }
            }).ConfigureAwait(false);
            await _diaryService.UpdateAsync(diaries, it => new { it.Content }).ConfigureAwait(false);
        }

        public async Task MergedDiariesAsync(List<DiaryModel> diaries)
        {
            if (diaries.Count < 2) return;

            diaries = diaries.OrderBy(it => it.CreateTime).ToList();
            string content = string.Join("\n", diaries.Select(it => it.Content));
            var tags = diaries
                .Where(d => d.Tags != null)
                .SelectMany(d => d.Tags ?? [])
                .DistinctBy(t => t.Name)
                .ToList();
            var resources = diaries
                .Where(d => d.Resources != null)
                .SelectMany(d => d.Resources ?? [])
                .DistinctBy(t => t.ResourceUri)
                .ToList();

            var firstDiary = diaries.First();
            firstDiary.Content = content;
            firstDiary.Tags = tags;
            firstDiary.Resources = resources;

            await _diaryService.UpdateIncludesAsync(firstDiary);
            diaries.Remove(firstDiary);
            await _diaryService.DeleteAsync(diaries);
        }
    }
}
