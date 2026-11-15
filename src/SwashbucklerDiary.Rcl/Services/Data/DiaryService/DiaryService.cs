using SqlSugar;
using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;
using System.Linq.Expressions;

namespace SwashbucklerDiary.Rcl.Services
{
#pragma warning disable CS0472 // 由于此类型的值永不等于 "null"，该表达式的结果始终相同
    public class DiaryService : BaseDataService<DiaryModel>, IDiaryService
    {
        private readonly IDiaryRepository _iDiaryRepository;

        private readonly ISyncGraveService _graveService;

        public DiaryService(IDiaryRepository iDiaryRepository, ISyncGraveService graveService)
        {
            base._iBaseRepository = iDiaryRepository;
            _iDiaryRepository = iDiaryRepository;
            _graveService = graveService;
        }

        // ---- 删除时记录同步墓碑 ----
        // 墓碑是删除唯一能跨设备传播的手段。同步自身应用远端删除时用
        // SyncContext.BeginSuppressGraveRecording() 抑制，否则删除会在两端来回震荡。

        // 顺序很重要：**先删除，成功后再写墓碑**。
        // 反过来的话，删除失败（数据库锁、磁盘满）会留下一条"对象还在、墓碑已写"的状态，
        // 下一次同步会把删除推到远端，随后远端墓碑再回来把本地也删掉——用户看到"删除失败"，
        // 实际结果却是两端数据都没了。墓碑写失败只会导致删除不传播，代价小得多。

        public override async Task<bool> DeleteAsync()
        {
            // 先查再删：墓碑需要具体 Id，而 SqlSugar 的批量删除不回传受影响的行。
            // 查询与删除之间的新增日记不在清单里，会漏墓碑；概率极低，接受。
            var diaries = await QueryAsync().ConfigureAwait(false);
            bool result = await base.DeleteAsync().ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(diaries).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(DiaryModel entity)
        {
            bool result = await base.DeleteAsync(entity).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync([entity]).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(Guid id)
        {
            bool result = await base.DeleteAsync(id).ConfigureAwait(false);
            if (result)
            {
                await RecordGraveAsync(id).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(List<DiaryModel> entities)
        {
            bool result = await base.DeleteAsync(entities).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(entities).ConfigureAwait(false);
            }

            return result;
        }

        public override async Task<bool> DeleteAsync(Expression<Func<DiaryModel, bool>> expression)
        {
            var diaries = await QueryAsync(expression).ConfigureAwait(false);
            bool result = await base.DeleteAsync(expression).ConfigureAwait(false);
            if (result)
            {
                await RecordGravesAsync(diaries).ConfigureAwait(false);
            }

            return result;
        }

        private async Task RecordGravesAsync(IEnumerable<DiaryModel> diaries)
        {
            if (SyncContext.SuppressGraveRecording)
            {
                return;
            }

            foreach (var diary in diaries)
            {
                await RecordGraveAsync(diary.Id).ConfigureAwait(false);
            }
        }

        private async Task RecordGraveAsync(Guid id)
        {
            if (SyncContext.SuppressGraveRecording || id == Guid.Empty)
            {
                return;
            }

            await _graveService.AddAsync(SyncEntityKeys.DiaryType, id).ConfigureAwait(false);
        }

        public override Task<List<DiaryModel>> QueryAsync()
        {
            return _iBaseRepository.GetListAsync();
        }

        public override Task<List<DiaryModel>> QueryAsync(Expression<Func<DiaryModel, bool>> expression)
        {
            return _iBaseRepository.GetListAsync(expression);
        }

        public Task<List<TagModel>> GetTagsAsync(Guid id)
        {
            return _iDiaryRepository.GetTagsAsync(id);
        }

        public Task<bool> UpdateIncludesAsync(DiaryModel model)
        {
            return _iDiaryRepository.UpdateIncludesAsync(model);
        }

        public Task<bool> UpdateIncludesAsync(List<DiaryModel> models)
        {
            return _iDiaryRepository.UpdateIncludesAsync(models);
        }

        public Task<bool> UpdateTagsAsync(DiaryModel model)
        {
            return _iDiaryRepository.UpdateTagsAsync(model);
        }

        public Task<bool> ImportAsync(List<DiaryModel> diaries)
        {
            return _iDiaryRepository.ImportAsync(diaries);
        }

        public Task<bool> MovePrivacyDiaryAsync(DiaryModel diary, bool toPrivacyMode)
        {
            return _iDiaryRepository.MovePrivacyDiaryAsync(diary, toPrivacyMode);
        }

        public Task<bool> MovePrivacyDiariesAsync()
        {
            return _iDiaryRepository.MovePrivacyDiariesAsync();
        }

        public Task<List<DiaryModel>> QueryDiariesAsync()
        {
            return QueryAsync(it => it.Template == null || it.Template == false);
        }

        public Task<List<DiaryModel>> QueryDiariesAsync(Expression<Func<DiaryModel, bool>> expression)
        {
            var expable = Expressionable.Create<DiaryModel>();
            expable.And(expression);
            expable.And(it => it.Template == null || it.Template == false);
            return QueryAsync(expable.ToExpression());
        }

        public Task<List<DiaryModel>> QueryTemplatesAsync()
        {
            return QueryAsync(it => it.Template);
        }

        public Task<List<DiaryModel>> QueryTemplatesAsync(Expression<Func<DiaryModel, bool>> expression)
        {
            var expable = Expressionable.Create<DiaryModel>();
            expable.And(expression);
            expable.And(it => it.Template);
            return QueryAsync(expable.ToExpression());
        }
    }
}
