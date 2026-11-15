using SqlSugar;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Repository
{
    /// <summary>
    /// 墓碑固定落在主库。
    /// <para>
    /// 不能沿用 <see cref="BaseRepository{T}"/> 的"跟随隐私模式切库"：隐私库首版不参与同步，
    /// 在隐私模式下删日记如果写进隐私库，这些墓碑永远不会被任何一端消费，只会越积越多。
    /// 固定主库的另一个好处是隐私模式开关不会影响同步台账的连续性。
    /// </para>
    /// </summary>
    public class SyncGraveRepository : BaseRepository<SyncGraveModel>, ISyncGraveRepository
    {
        public SyncGraveRepository(ISqlSugarClient context, ISettingService settingService)
            : base(context, settingService)
        {
        }

        public override ISqlSugarClient Context
        {
            get => Itenant.GetConnection(SQLiteConstants.MainDatabaseFilename);
            set => base.Context = value;
        }
    }
}
