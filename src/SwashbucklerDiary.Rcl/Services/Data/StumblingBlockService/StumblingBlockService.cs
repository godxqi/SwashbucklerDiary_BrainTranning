using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class StumblingBlockService : IStumblingBlockService
    {
        private readonly IReviewRepository<StumblingBlockModel> _blockRepository;

        private readonly IDiaryService _diaryService;

        private readonly IReviewScheduleService _scheduleService;

        private readonly IArchiveService _archiveService;

        public StumblingBlockService(IReviewRepository<StumblingBlockModel> blockRepository,
            IDiaryService diaryService,
            IReviewScheduleService scheduleService,
            IArchiveService archiveService)
        {
            _blockRepository = blockRepository;
            _diaryService = diaryService;
            _scheduleService = scheduleService;
            _archiveService = archiveService;
        }

        public async Task SyncFromDailyAsync(Guid diaryId, DateTime date, IReadOnlyList<string[]> microFailures)
        {
            if (diaryId == Guid.Empty || microFailures is null)
            {
                return;
            }

            var (_, _, year, weekOfYear) = _scheduleService.GetWeek(date);

            var existing = await _blockRepository.Queryable()
                .Where(it => it.SourceDiaryId == diaryId)
                .ToListAsync()
                .ConfigureAwait(false);

            //同一序号理论上只有一行；真有重复时保留最早的那行，多余的先清掉。
            //按序号 + 创建时间排序，后面按描述认领时「同一描述有多条」才有稳定的先后。
            var ordered = new List<StumblingBlockModel>();
            foreach (var item in existing.OrderBy(it => it.OrderIndex).ThenBy(it => it.CreateTime))
            {
                if (ordered.Any(it => it.OrderIndex == item.OrderIndex))
                {
                    await _blockRepository.DeleteByIdAsync(item.Id).ConfigureAwait(false);
                    continue;
                }

                ordered.Add(item);
            }

            var descriptions = new string[microFailures.Count];
            for (int i = 0; i < microFailures.Count; i++)
            {
                descriptions[i] = Describe(microFailures[i]);
            }

            var claimed = new HashSet<Guid>();
            var bound = new StumblingBlockModel?[descriptions.Length];

            //第一轮：按描述文本认领。
            //这是「随时回改过去某天的日课」的关键——中间插一条、删一条、换个顺序，
            //判定都跟着它描述的那条走，而不会因为序号顺移就静默地指到别人身上。
            for (int i = 0; i < descriptions.Length; i++)
            {
                if (descriptions[i].Length == 0)
                {
                    continue;
                }

                var match = ordered.FirstOrDefault(it => !claimed.Contains(it.Id)
                    && string.Equals(it.Description, descriptions[i], StringComparison.CurrentCulture));
                if (match is null)
                {
                    continue;
                }

                bound[i] = match;
                claimed.Add(match.Id);
            }

            //第二轮：剩下没认到的，退回按序号认领。
            //描述被整体改写时（旧行一条都对不上）就靠这一步把位置接住。
            for (int i = 0; i < descriptions.Length; i++)
            {
                if (bound[i] is not null)
                {
                    continue;
                }

                var match = ordered.FirstOrDefault(it => !claimed.Contains(it.Id) && it.OrderIndex == i);
                if (match is null)
                {
                    continue;
                }

                bound[i] = match;
                claimed.Add(match.Id);
            }

            for (int i = 0; i < descriptions.Length; i++)
            {
                string description = descriptions[i];
                var block = bound[i];

                if (block is null)
                {
                    if (description.Length == 0)
                    {
                        continue;
                    }

                    await _blockRepository.InsertAsync(new StumblingBlockModel
                    {
                        Id = Guid.NewGuid(),
                        Year = year,
                        WeekOfYear = weekOfYear,
                        Description = description,
                        SourceDiaryId = diaryId,
                        OrderIndex = i,
                        Verdict = BlockVerdict.Undecided,
                    }).ConfigureAwait(false);
                    continue;
                }

                if (description.Length == 0 && !IsJudged(block))
                {
                    //没写也没判过：这行没有意义了，删掉
                    await _blockRepository.DeleteByIdAsync(block.Id).ConfigureAwait(false);
                    continue;
                }

                //只动「位置 + 周次 + 描述」。Verdict / Category 是周课的人工判断，
                //而且描述清空但判过的那条要留下旧描述，否则周课里那条判断就失去了所指。
                block.OrderIndex = i;
                block.Year = year;
                block.WeekOfYear = weekOfYear;
                if (description.Length > 0)
                {
                    block.Description = description;
                }

                block.UpdateTime = DateTime.Now;

                await _blockRepository.UpdateAsync(block,
                    it => new { it.OrderIndex, it.Year, it.WeekOfYear, it.Description, it.UpdateTime })
                    .ConfigureAwait(false);
            }

            //没人认领的剩货：没判过的清掉；判过的留着——
            //那是周课已经做过的人工判断，不该因为日课那边删了一条就无声消失。
            //（描述还在日课里找得到的那些，上一轮就已经被认领走了，不会落到这里。）
            var stale = ordered
                .Where(it => !claimed.Contains(it.Id))
                .Where(it => !IsJudged(it))
                .ToList();

            if (stale.Count > 0)
            {
                await _blockRepository.DeleteAsync(stale).ConfigureAwait(false);
            }
        }

        public async Task DeleteBySourceDiaryAsync(Guid diaryId)
        {
            if (diaryId == Guid.Empty)
            {
                return;
            }

            var rows = await _blockRepository.Queryable()
                .Where(it => it.SourceDiaryId == diaryId)
                .ToListAsync()
                .ConfigureAwait(false);

            if (rows.Count > 0)
            {
                await _blockRepository.DeleteAsync(rows).ConfigureAwait(false);
            }
        }

        public async Task<List<StumblingBlockView>> GetByWeekAsync(int year, int weekOfYear)
        {
            var blocks = await _blockRepository.Queryable()
                .Where(it => it.Year == year && it.WeekOfYear == weekOfYear)
                .ToListAsync()
                .ConfigureAwait(false);

            return await ToViewsAsync(blocks).ConfigureAwait(false);
        }

        public async Task<List<StumblingBlockView>> GetByMonthAsync(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            //把这一月的每一天归到它所属的 ISO 周，再按 ISO 年收成连续区间。
            //同一个 ISO 年内的周次是单调递增的，所以 min ~ max 就是精确的周次集合；
            //跨年（如 12 月到 1 月）会得到两段，各自查一次。
            var spans = new List<(int Year, int Min, int Max)>();
            var byIsoYear = new Dictionary<int, (int Min, int Max)>();

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var (_, _, weekYear, weekOfYear) = _scheduleService.GetWeek(day);

                byIsoYear[weekYear] = byIsoYear.TryGetValue(weekYear, out var span)
                    ? (Math.Min(span.Min, weekOfYear), Math.Max(span.Max, weekOfYear))
                    : (weekOfYear, weekOfYear);
            }

            spans.AddRange(byIsoYear.Select(it => (it.Key, it.Value.Min, it.Value.Max)));

            var blocks = new List<StumblingBlockModel>();

            foreach (var span in spans)
            {
                var part = await _blockRepository.Queryable()
                    .Where(it => it.Year == span.Year)
                    .Where(it => it.WeekOfYear >= span.Min && it.WeekOfYear <= span.Max)
                    .ToListAsync()
                    .ConfigureAwait(false);

                blocks.AddRange(part);
            }

            return await ToViewsAsync(blocks).ConfigureAwait(false);
        }

        /// <summary>补上来源日期并排序。周课与月课共用。</summary>
        private async Task<List<StumblingBlockView>> ToViewsAsync(List<StumblingBlockModel> blocks)
        {
            if (blocks.Count == 0)
            {
                return [];
            }

            var dates = await GetSourceDatesAsync(blocks).ConfigureAwait(false);

            return [.. blocks
                .OrderBy(it => dates.TryGetValue(it.SourceDiaryId ?? Guid.Empty, out var day) ? day : DateTime.MaxValue)
                .ThenBy(it => it.OrderIndex)
                //序号的最后一道保险：7.4 回流出来的行没有来源日记、序号也一律记 0，
                //光靠前两个键它们之间排序就是随机的——用创建时间兜底，顺序才是稳定的
                .ThenBy(it => it.CreateTime)
                .Select(it => new StumblingBlockView(it,
                    dates.TryGetValue(it.SourceDiaryId ?? Guid.Empty, out var day) ? day : null))];
        }

        public async Task<List<string>> GetKnownCategoriesAsync()
        {
            var categories = await _blockRepository.Queryable()
                .Where(it => it.Category != null && it.Category != "")
                .Select(it => it.Category!)
                .Distinct()
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. categories.OrderBy(it => it, StringComparer.CurrentCulture)];
        }

        private Task<bool> UpdateAsync(StumblingBlockModel block)
        {
            block.UpdateTime = DateTime.Now;
            return _blockRepository.UpdateAsync(block,
                it => new { it.Category, it.Verdict, it.UpdateTime });
        }

        public async Task<bool> JudgeAsync(StumblingBlockModel block, BlockVerdict verdict, string? category)
        {
            //只有「第一次离开未归档状态」才落归档区。改来改去的判定不该把 7.4 的次数刷高——
            //OccurrenceCount 的含义是「这条东西又出现了」，不是「用户点了几次」。
            //
            //已知行为：判成「环境原因」之后又改判成别的，先前写进 7.4 的那条**不会撤销**。
            //宁可多留一条归档（人可以在归档区里自己删），也不要在改判时误删用户真正想留的东西。
            bool wasArchived = block.Verdict is BlockVerdict.Environment or BlockVerdict.Rejected;

            block.Verdict = verdict;
            block.Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();

            if (!await UpdateAsync(block).ConfigureAwait(false))
            {
                return false;
            }

            if (wasArchived)
            {
                return true;
            }

            ArchiveSource? source = verdict switch
            {
                BlockVerdict.Environment => ArchiveSource.EnvironmentFailure,
                BlockVerdict.Rejected => ArchiveSource.RejectedBlock,
                _ => null,
            };

            if (source is ArchiveSource archiveSource)
            {
                await _archiveService
                    .ArchiveAsync(ArchiveKind.MicroFailure, archiveSource, block.Description)
                    .ConfigureAwait(false);
            }

            return true;
        }

        /// <summary>四问里的第一问「发生了什么？」就是周课 2.1 要列出来的那条失败。</summary>
        private static string Describe(string[] answers)
        {
            return (answers.ElementAtOrDefault(0) ?? string.Empty).Trim();
        }

        /// <summary>是否已经有人（周课）动过这一行。这类行一律不删。</summary>
        private static bool IsJudged(StumblingBlockModel block)
        {
            return block.Verdict != BlockVerdict.Undecided || !string.IsNullOrWhiteSpace(block.Category);
        }

        /// <summary>失败没有自己的日期，日期在它来源的那篇日课日记上。</summary>
        private async Task<Dictionary<Guid, DateTime>> GetSourceDatesAsync(List<StumblingBlockModel> blocks)
        {
            var ids = blocks
                .Where(it => it.SourceDiaryId.HasValue)
                .Select(it => it.SourceDiaryId!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
            {
                return [];
            }

            var diaries = await _diaryService
                .QueryAsync(it => ids.Contains(it.Id))
                .ConfigureAwait(false);

            return diaries
                .GroupBy(it => it.Id)
                .ToDictionary(it => it.Key, it => it.First().CreateTime.Date);
        }
    }
}
