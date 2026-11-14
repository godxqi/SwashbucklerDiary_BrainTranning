namespace SwashbucklerDiary.Shared
{
    /// <summary>
    /// 三个优点表（方法文档 6）的一条。这一组表的用法只有一个：在状态糟糕的时候被调用。
    /// </summary>
    public class VirtueEntryModel : BaseModel
    {
        public VirtueKind Kind { get; set; }

        public string? Content { get; set; }

        public DateTime Date { get; set; }

        public Guid? SourceDiaryId { get; set; }

        /// <summary>来自哪篇课题（课题存档的归档块）。空表示不是从课题来的。</summary>
        public Guid? SourceLessonId { get; set; }

        /// <summary>
        /// 「这条是达成的成就」。<see cref="VirtueKind.Achievement"/> 就是它，所以语义上可推导。
        /// <para>
        /// <b>但这两个字段删不得</b>：老库的 <c>VirtueEntryModel</c> 表里有
        /// <c>IsAchieved bit NOT NULL</c> 这一列，而 SqlSugar 的 <c>InitTables</c>
        /// <b>只加列、从不删列、也不改可空性</b>。模型里一旦没有这个属性，
        /// INSERT 就不会带上它，SQLite 直接
        /// <c>NOT NULL constraint failed: VirtueEntryModel.IsAchieved</c> —— 往优点表插一条都插不进去。
        /// （2026-10-06 真机实测抓到，阶段 5 删字段时漏算了这一点。）
        /// </para>
        /// <para>
        /// 想彻底去掉的话，得另加一次 <c>ALTER TABLE ... DROP COLUMN</c> 的迁移，
        /// 属于改数据结构，得先跟主人打招呼。
        /// </para>
        /// </summary>
        public bool IsAchieved { get; set; }

        /// <summary>达成时间。理由同 <see cref="IsAchieved"/>——老库里那一列还在。</summary>
        public DateTime? AchievedTime { get; set; }
    }
}
