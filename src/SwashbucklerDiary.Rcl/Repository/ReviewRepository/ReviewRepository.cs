using SqlSugar;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Rcl.Repository
{
    public class ReviewRepository<T> : BaseRepository<T>, IReviewRepository<T> where T : class, new()
    {
        public ReviewRepository(ISqlSugarClient context,
            ISettingService settingService) : base(context, settingService)
        {
        }

        public ISugarQueryable<T> Queryable()
        {
            return Context.Queryable<T>();
        }
    }
}
