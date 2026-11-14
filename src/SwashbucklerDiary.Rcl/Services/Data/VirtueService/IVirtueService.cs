using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 成就列表按月长出来的样子。文档 6.3 的原话是「观察成就列表的增长情况，
    /// 并将其作为指导，以了解是否需要加强、删除或添加某些内容」——所以「增长」要能看得出来。
    /// </summary>
    public sealed record AchievementGrowth(int Year, int Month, int Count);

    /// <summary>
    /// 三个优点表（方法文档 6）。这一组表的用法只有一个：<b>在状态糟糕的时候被调用</b>。
    /// <para>
    /// 自动写入口只有一个：周课 2.4 的目标达成 → 成就列表；
    /// 其余靠手填（文档 6.1 的积极列表、6.2 的感恩列表本来就是手写的）。
    /// </para>
    /// </summary>
    public interface IVirtueService
    {
        Task<List<VirtueEntryModel>> QueryAsync(VirtueKind kind);

        /// <summary>
        /// 记一条。<see cref="VirtueKind.Achievement"/> 会顺手打上达成标记。
        /// <para>
        /// <paramref name="date"/> 是这条归属的日期，补足过去的优点表时由页面给；<b>不传就是「现在记的」</b>。
        /// </para>
        /// </summary>
        Task<bool> AddAsync(VirtueKind kind, string? content, DateTime? date = null,
            Guid? sourceDiaryId = null, Guid? sourceLessonId = null);

        Task<bool> UpdateAsync(VirtueEntryModel entry);

        Task<bool> DeleteAsync(Guid id);

        /// <summary>成就列表按月的条数，从早到晚。统计页（阶段 4）与成就页顶部的增长小计都用它。</summary>
        Task<List<AchievementGrowth>> GetAchievementGrowthAsync();
    }
}
