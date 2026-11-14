using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>列表里的一条课题：记录本身，以及人物、触因、类型解析后的样子。</summary>
    public sealed record LessonView(
        LessonModel Lesson,
        string? PersonName,
        string? TriggerName,
        List<LessonType> Types);

    public sealed record LessonTypeCount(LessonType Type, int Count);

    public sealed record LessonPersonCount(Guid PersonTagId, string? PersonName, int Count);

    /// <summary>
    /// 课题记录（方法文档 4）。它不再是日记：内容全在 <see cref="LessonModel"/> 的列里，
    /// 人物与触因指向标签，类型落在 <see cref="LessonTypeLinkModel"/>。
    /// </summary>
    public interface ILessonService
    {
        Task<List<LessonView>> QueryAsync(
            Guid? personTagId = null,
            LessonType? type = null,
            bool dueOnly = false,
            DateTime? now = null);

        Task<LessonView?> GetAsync(Guid id);

        /// <summary>全文搜索：事件、照见、边界、带走一句、下一步，以及人物名字。</summary>
        Task<List<LessonView>> SearchAsync(string keyword);

        /// <summary>新建或更新一条，连同类型关联一起写；返回 Id，失败返回 null。</summary>
        Task<Guid?> SaveAsync(LessonModel lesson, IReadOnlyCollection<LessonType> types);

        Task<bool> DeleteAsync(Guid id);

        /// <summary>按类型计数；给了人物就只数那个人的课题。</summary>
        Task<List<LessonTypeCount>> GetTypeCountsAsync(Guid? personTagId = null);

        /// <summary>某个人的「当前边界」：最近一条 Boundary 非空的课题。</summary>
        Task<LessonView?> GetLatestBoundaryByPersonAsync(Guid personTagId);

        /// <summary>到期且还没回访的课题。</summary>
        Task<List<LessonView>> GetDueFollowUpsAsync(DateTime now);

        /// <summary>月课 3.1 候选：本月同类出现 ≥ <paramref name="minCount"/> 次的类型。</summary>
        Task<List<LessonTypeCount>> GetMonthlyCandidateTypesAsync(DateTime month, int minCount = 2);

        /// <summary>月课 3.1 候选：本月同一人物出现 ≥ <paramref name="minCount"/> 次的人物。</summary>
        Task<List<LessonPersonCount>> GetMonthlyCandidatePersonsAsync(DateTime month, int minCount = 3);
    }
}
