namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// 复盘数据的可读导出（需求 6 的缺口：JSON/MD/TXT 导出以前只带日记本体）。
    /// <para>
    /// 与整库备份（<c>ExportDBAsync</c>）的分工：整库备份复制整个 .db3，
    /// 复盘数据本来就在里面，所以那条路一直是安全的；这里补的是**可读导出**——
    /// 用户拿走一个 JSON 包换设备时，结构化数据不该丢。
    /// </para>
    /// </summary>
    public interface IReviewExportService
    {
        /// <summary>
        /// 读出 12 张表的全部内容。隐私模式下读到的是隐私库那一份——
        /// 仓储自己会按 <c>SettingService</c> 的临时态切连接，和日记导出走哪一份库保持一致。
        /// </summary>
        Task<ReviewExportModel> ExportAsync();

        /// <summary>
        /// 按主键 upsert 导入：库里没有的插进去，已经有的覆盖掉。
        /// <para>
        /// 之所以不做 Id 重映射：这 12 张表之间的外键（<c>WeeklyReviewId</c> /
        /// <c>MonthlyReviewId</c> / <c>DiaryId</c> / <c>SourceDiaryId</c> …）全是裸 Guid，
        /// 而 Guid 是跟着数据一起走的——保持原 Id 就能让整张引用图原样自洽，
        /// 比「重新生成 Id 再逐张表改外键」省一个数量级的出错机会。
        /// </para>
        /// </summary>
        Task ImportAsync(ReviewExportModel? model);
    }
}
