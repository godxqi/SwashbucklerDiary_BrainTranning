using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 状态管理表列表里的一条：记录本身，外加它引用的两个跨表对象解析后的文字。
    /// 触因名在 <see cref="TagModel"/> 里，「调用哪一条」在三个优点表 / 肯定库 / 宏观目标里，
    /// 页面要显示就得跨表取——收在这里做，免得每个页面各拼一次。
    /// </summary>
    public sealed record StateRecordView(
        StateRecordModel Record,
        string? TriggerName,
        string? InvokedText);

    /// <summary>
    /// 状态管理表（方法文档 5）。
    /// <para>
    /// 它不是每日节拍的一部分——日课是「睡前 10 分钟」，这张表是「状态糟糕时随手记」。
    /// 出口只有一个（5.2）：<b>同类触因攒够 3 个，交给月课 3.1 的第一列</b>，
    /// 那一步靠 <see cref="ITriggerService"/> 数次数，所以这里的触因必须是
    /// <see cref="TagModel"/>（Kind = Trigger）而不是自由文本。
    /// </para>
    /// </summary>
    public interface IStateRecordService
    {
        /// <summary>全部记录，按发生时间从新到旧。列表页用。</summary>
        Task<List<StateRecordView>> QueryAsync();

        Task<StateRecordView?> GetAsync(Guid id);

        /// <summary>
        /// 新建或更新一条，返回它的 Id；整条都是空的则拒绝保存并返回 <c>null</c>。
        /// <para>
        /// 「只有触因、别的都没填」<b>不算空</b>——那正是触因页「＋ 记一次」的产物，
        /// 用户在完整表单里再保存一次不该被拒。真正被拒的是连触因都没有的空壳。
        /// </para>
        /// </summary>
        Task<Guid?> SaveAsync(StateRecordModel record);

        Task<bool> DeleteAsync(Guid id);
    }
}
