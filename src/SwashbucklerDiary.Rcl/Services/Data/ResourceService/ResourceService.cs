using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;
using System.Linq.Expressions;

namespace SwashbucklerDiary.Rcl.Services
{
    public class ResourceService : BaseDataService<ResourceModel>, IResourceService
    {
        protected readonly IResourceRepository _resourceRepository;

        protected readonly IMediaResourceManager _mediaResourceManager;

        protected readonly ISettingService _settingService;

        protected readonly IAppFileSystem _appFileSystem;

        protected readonly ISyncGraveService _graveService;

        public ResourceService(IResourceRepository resourceRepository,
            IMediaResourceManager mediaResourceManager,
            ISettingService settingService,
            IAppFileSystem appFileSystem,
            ISyncGraveService graveService)
        {
            base._iBaseRepository = resourceRepository;
            _resourceRepository = resourceRepository;
            _mediaResourceManager = mediaResourceManager;
            _settingService = settingService;
            _appFileSystem = appFileSystem;
            _graveService = graveService;
        }

        public async Task DeleteUnusedResourcesAsync(Expression<Func<ResourceModel, bool>> expression)
        {
            await _resourceRepository.DeleteUnusedResourcesAsync(expression).ConfigureAwait(false);
        }

        public async Task DeleteAllUnusedResourcesWithFilesAsync()
        {
            await _resourceRepository.DeleteUnusedResourcesAsync().ConfigureAwait(false);

            bool privacyMode = _settingService.GetTemp(it => it.PrivacyMode);
            var trulyUsedResourceUris = await _resourceRepository.QueryTrulyUsedResourcesAsync(privacyMode).ConfigureAwait(false);
            await DeleteResourceFiles(trulyUsedResourceUris).ConfigureAwait(false);
        }

        public Task<ResourceModel> FindIncludesAsync(string id)
        {
            return _resourceRepository.FindIncludesAsync(id);
        }

        public Task<List<Guid>> GetDiaryIdsAsync(string id)
        {
            return _resourceRepository.GetDiaryIdsAsync(id);
        }

        private async Task DeleteResourceFiles(List<string> resourceUris)
        {
            var filePaths = resourceUris
                .Select(it => _mediaResourceManager.RelativeUrlToFilePath(it))
                .Where(it => !string.IsNullOrEmpty(it))
                .ToList();

            // 删除前把这些文件记成同步墓碑，否则另一端的附件永远删不掉。
            // 隐私模式跳过：引用查询只看当前库，而附件目录是两库共享的，
            // 此时写墓碑可能把主库仍在引用的文件从另一端删掉。
            bool privacyMode = _settingService.GetTemp(it => it.PrivacyMode);
            if (!SyncContext.SuppressGraveRecording && !privacyMode)
            {
                var keepPaths = filePaths
                    .Select(Path.GetFullPath)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var (kind, directoryPath) in _mediaResourceManager.MediaResourceDirectoryPaths)
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        continue;
                    }

                    foreach (string file in Directory.EnumerateFiles(
                                 directoryPath, "*", SearchOption.AllDirectories))
                    {
                        if (keepPaths.Contains(Path.GetFullPath(file)))
                        {
                            continue;
                        }

                        await _graveService.AddAsyncByKeyAsync(
                            SyncEntityKeys.AssetType,
                            SyncAssetPath.ToBareKey(kind, directoryPath, file)).ConfigureAwait(false);
                    }
                }
            }

            foreach (var directoryPath in _mediaResourceManager.MediaResourceDirectoryPaths.Values)
            {
                await _appFileSystem.ClearFolderAsync(directoryPath, filePaths).ConfigureAwait(false);
            }
        }
    }
}
