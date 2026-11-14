using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public interface ITagService : IBaseDataService<TagModel>
    {
        Task<TagModel> FindIncludesAsync(Guid id);

        Task<Dictionary<Guid, int>> TagsDiaryCount();

        /// <summary>
        /// 按名字拿人物标签：没有就新建；同名的普通标签顺手升级成 Person。
        /// 名字为空返回 null。
        /// </summary>
        Task<TagModel?> GetOrCreatePersonAsync(string? name);
    }
}
