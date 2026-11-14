using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class TagService : BaseDataService<TagModel>, ITagService
    {
        private readonly ITagRepository _tagRepository;

        public TagService(ITagRepository tagRepository)
        {
            base._iBaseRepository = tagRepository;
            _tagRepository = tagRepository;
        }

        public Task<TagModel> FindIncludesAsync(Guid id)
        {
            return _tagRepository.GetByIdIncludesAsync(id);
        }

        public Task<Dictionary<Guid, int>> TagsDiaryCount()
        {
            return _tagRepository.TagsDiaryCount();
        }

        public async Task<TagModel?> GetOrCreatePersonAsync(string? name)
        {
            string value = name?.Trim() ?? string.Empty;
            if (value.Length == 0)
            {
                return null;
            }

            var tag = await FindAsync(it => it.Name == value
                && (it.Kind == TagKind.Person || it.Kind == TagKind.Normal || it.Kind == null));

            if (tag is not null)
            {
                if (tag.Kind != TagKind.Person)
                {
                    //同名旧普通标签直接升级，不另造一个「张三」，否则历史会被劈成两半
                    tag.Kind = TagKind.Person;
                    tag.UpdateTime = DateTime.Now;
                    await UpdateAsync(tag, it => new { it.Kind, it.UpdateTime });
                }

                return tag;
            }

            tag = new TagModel
            {
                Id = Guid.NewGuid(),
                Name = value,
                Kind = TagKind.Person,
            };
            return await AddAsync(tag) ? tag : null;
        }
    }
}
