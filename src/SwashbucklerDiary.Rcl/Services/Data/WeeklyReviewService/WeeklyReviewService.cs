using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class WeeklyReviewService : IWeeklyReviewService
    {
        private readonly IReviewRepository<WeeklyReviewModel> _weeklyReviewRepository;

        private readonly IReviewRepository<WeeklyGoalModel> _goalRepository;

        private readonly IStumblingBlockService _stumblingBlockService;

        private readonly IVirtueService _virtueService;

        private readonly IReviewScheduleService _scheduleService;

        private readonly IDiaryService _diaryService;

        private readonly IMediaResourceManager _mediaResourceManager;

        private readonly II18nService _i18n;

        public WeeklyReviewService(IReviewRepository<WeeklyReviewModel> weeklyReviewRepository,
            IReviewRepository<WeeklyGoalModel> goalRepository,
            IStumblingBlockService stumblingBlockService,
            IVirtueService virtueService,
            IReviewScheduleService scheduleService,
            IDiaryService diaryService,
            IMediaResourceManager mediaResourceManager,
            II18nService i18n)
        {
            _weeklyReviewRepository = weeklyReviewRepository;
            _goalRepository = goalRepository;
            _stumblingBlockService = stumblingBlockService;
            _virtueService = virtueService;
            _scheduleService = scheduleService;
            _diaryService = diaryService;
            _mediaResourceManager = mediaResourceManager;
            _i18n = i18n;
        }

        public async Task<WeeklyReviewModel?> GetAsync(int year, int weekOfYear)
        {
            return await _weeklyReviewRepository.Queryable()
                .Where(it => it.Year == year && it.WeekOfYear == weekOfYear)
                .FirstAsync()
                .ConfigureAwait(false);
        }

        public async Task<WeeklyReviewModel> GetOrCreateAsync(DateTime date)
        {
            var (start, end, year, weekOfYear) = _scheduleService.GetWeek(date);

            var existing = await GetAsync(year, weekOfYear).ConfigureAwait(false);
            if (existing is not null)
            {
                return existing;
            }

            var created = new WeeklyReviewModel
            {
                Id = Guid.NewGuid(),
                Year = year,
                WeekOfYear = weekOfYear,
                StartDate = start,
                EndDate = end,
            };

            await _weeklyReviewRepository.InsertAsync(created).ConfigureAwait(false);
            return created;
        }

        public async Task<bool> SaveAsync(WeeklyReviewModel review, WeeklyReviewSections sections)
        {
            //「数频率」与「三个目标」这两节是不是已经动过手。
            //这两个开关目前只做记录，节拍判定不看它们（判定看 IsFinalized）。
            var blocks = await _stumblingBlockService
                .GetByWeekAsync(review.Year, review.WeekOfYear)
                .ConfigureAwait(false);
            var goals = await GetGoalsByReviewAsync(review.Id).ConfigureAwait(false);

            review.FrequencyConfirmed = blocks.Any(it => it.Block.Verdict != BlockVerdict.Undecided
                || !string.IsNullOrWhiteSpace(it.Block.Category));
            review.GoalsConfirmed = goals.Count > 0;

            var existingDiary = await FindDiaryAsync(review).ConfigureAwait(false);

            //什么都没写、也没定目标没判定、而且这一周还没建过日记：不建空日记。
            //已经建过就一定要往下走——用户可能是把之前写的散文清空了，正文得跟着清。
            if (sections.IsEmpty && existingDiary is null && !review.FrequencyConfirmed && !review.GoalsConfirmed)
            {
                return true;
            }

            var diary = existingDiary ?? await CreateDiaryAsync(review).ConfigureAwait(false);
            if (diary is null)
            {
                //日记没建起来就不该继续写 DiaryId，否则留下孤儿引用
                return false;
            }

            string content = sections.Compose(
                ReviewSectionText.WeeklyTitles(_i18n),
                ReviewSectionText.ExitQuestions(_i18n));

            diary.Kind = ReviewKind.Weekly;
            diary.Template = false;
            diary.Title = $"{_i18n.T("Weekly review")} {review.StartDate:yyyy/MM/dd} - {review.EndDate:MM/dd}";
            diary.Content = ReviewContentBlock.Apply(content, BuildFacetBlock(review, blocks, goals));
            diary.Resources = _mediaResourceManager.GetDiaryResources(diary.Content);
            diary.UpdateTime = DateTime.Now;

            if (!await _diaryService.UpdateIncludesAsync(diary).ConfigureAwait(false))
            {
                return false;
            }

            review.DiaryId = diary.Id;
            //写了东西就不再算「跳过」——跳过的意思是「这段我真的没写」，写了就不成立了
            review.IsSkipped = false;
            review.UpdateTime = DateTime.Now;

            return await _weeklyReviewRepository.UpdateAsync(review).ConfigureAwait(false);
        }

        public async Task<bool> SetSkippedAsync(WeeklyReviewModel review, bool skipped)
        {
            review.IsSkipped = skipped;
            review.UpdateTime = DateTime.Now;

            return await _weeklyReviewRepository
                .UpdateAsync(review, it => new { it.IsSkipped, it.UpdateTime })
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 周课的结构化产物在正文里的「文字化统计」。
        /// <para>
        /// 周课和日课不一样：日课有 1.1~1.5 五段散文打底，芯片产物只是补充；周课的 2.1~2.4
        /// 全是判定、目标、结算这些结构化数据，正文里只有 2.5 / 2.6 两段散文。不补这一步，
        /// 把这篇日记单独导出成 Markdown 就只剩两个问题在，一条结论都看不见。
        /// </para>
        /// <para>
        /// 因为是每次保存重新生成的，所以不会出现「文案改了、库里那份还是旧的」。
        /// 区块之外用户自己补写的内容由 <see cref="ReviewContentBlock.Apply"/> 原样保留。
        /// </para>
        /// </summary>
        private string? BuildFacetBlock(
            WeeklyReviewModel review,
            List<StumblingBlockView> blocks,
            List<WeeklyGoalModel> goals)
        {
            List<string> lines = [];

            if (blocks.Count > 0)
            {
                //只列真正出现过的判定，四种都写上一遍（哪怕 0 条）反而更难读
                List<string> parts = [];
                foreach (var verdict in new[]
                {
                    BlockVerdict.StumblingBlock,
                    BlockVerdict.Environment,
                    BlockVerdict.Rejected,
                    BlockVerdict.Undecided,
                })
                {
                    int count = blocks.Count(it => it.Block.Verdict == verdict);
                    if (count > 0)
                    {
                        parts.Add($"{ReviewSectionText.VerdictText(_i18n, verdict)} {count}");
                    }
                }

                lines.Add($"- **{_i18n.T("Micro failure")}**: {blocks.Count}（{string.Join(" / ", parts)}）");
            }

            foreach (var goal in goals.Where(it => !string.IsNullOrWhiteSpace(it.Content)))
            {
                string domain = ReviewSectionText.DomainText(_i18n, goal.Domain);
                string status = ReviewSectionText.StatusText(_i18n, goal.Status);
                lines.Add($"- **{domain}**: {goal.Content}（{status}）");
            }

            if (review.IsFinalized)
            {
                lines.Add($"- **{_i18n.T("Weekly review")}**: {_i18n.T("Completed")}");
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }

        public async Task<WeeklyReviewSections> GetSectionsAsync(int year, int weekOfYear)
        {
            var review = await GetAsync(year, weekOfYear).ConfigureAwait(false);
            if (review?.DiaryId is not Guid diaryId || diaryId == Guid.Empty)
            {
                return new WeeklyReviewSections();
            }

            var diary = await _diaryService.FindAsync(diaryId).ConfigureAwait(false);
            //正文里可能还带着受管区块（阶段 4 会往里塞文字化统计），解析前先剥掉
            return WeeklyReviewSections.Parse(ReviewContentBlock.Strip(diary?.Content));
        }

        public async Task<List<WeeklyGoalModel>> GetGoalsAsync(int year, int weekOfYear)
        {
            var review = await GetAsync(year, weekOfYear).ConfigureAwait(false);
            if (review is null)
            {
                //那一周压根没开过周课，自然也没有目标——不是错误
                return [];
            }

            return await GetGoalsByReviewAsync(review.Id).ConfigureAwait(false);
        }

        public async Task<List<WeeklyGoalModel>> GetCarriedOverGoalsAsync(DateTime beforeDate)
        {
            var cutoff = beforeDate.Date;

            //先圈出「已经过完的那些周」，再在它们里面找还挂着的目标。
            //两步查询而不是 join：WeeklyGoalModel 上只有 WeeklyReviewId，
            //直接按 EndDate 过滤要先知道有哪些周。
            var reviewIds = await _weeklyReviewRepository.Queryable()
                .Where(it => it.EndDate < cutoff)
                .Select(it => it.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            if (reviewIds.Count == 0)
            {
                return [];
            }

            var goals = await _goalRepository.Queryable()
                .Where(it => reviewIds.Contains(it.WeeklyReviewId))
                .Where(it => it.Status == GoalStatus.Pending || it.Status == GoalStatus.InProgress)
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. goals.OrderBy(it => it.CreateTime)];
        }

        public async Task<bool> SaveGoalAsync(WeeklyGoalModel goal)
        {
            goal.UpdateTime = DateTime.Now;

            if (goal.Id == Guid.Empty)
            {
                goal.Id = Guid.NewGuid();
                return await _goalRepository.InsertAsync(goal).ConfigureAwait(false);
            }

            return await _goalRepository.UpdateAsync(goal).ConfigureAwait(false);
        }

        public Task<bool> DeleteGoalAsync(Guid id)
        {
            return _goalRepository.DeleteByIdAsync(id);
        }

        public async Task<bool> SettleGoalAsync(WeeklyGoalModel goal,
            DateTime settledDate,
            GoalStatus status)
        {
            bool wasDone = goal.Status == GoalStatus.Done;

            //结算记在「正在做的这一周」，不是「现在这一周」——回过去补周课时要一致
            var (_, _, year, weekOfYear) = _scheduleService.GetWeek(settledDate);

            goal.Status = status;
            goal.SettledYear = year;
            goal.SettledWeekOfYear = weekOfYear;
            goal.UpdateTime = DateTime.Now;

            bool success = await _goalRepository.UpdateAsync(goal,
                it => new
                {
                    it.Status,
                    it.SettledYear,
                    it.SettledWeekOfYear,
                    //未达成的归因也走这一趟：它是整行里唯一会被反复改的部分
                    it.SmartSpecificNote,
                    it.SmartMeasurableNote,
                    it.SmartAchievableNote,
                    it.SmartRelevantNote,
                    it.SmartTimeBoundNote,
                    it.UpdateTime,
                })
                .ConfigureAwait(false);

            if (!success)
            {
                return false;
            }

            //2.1 达成 → 写进成就列表（6.3）。只在「这次才变成达成」时写，
            //否则来回改主意会把成就列表刷成一堆重复条目。
            if (status == GoalStatus.Done && !wasDone)
            {
                await _virtueService
                    .AddAsync(VirtueKind.Achievement, goal.Content)
                    .ConfigureAwait(false);
            }

            //2026-10-06 起，未达成**不再**写 7.4 归档：归因改由 SMART 五项承担，
            //不再单独问「是不是环境原因」。7.4 仍由 2.2 的落选绊脚石写进去。

            return true;
        }

        public async Task<bool> FinalizeAsync(WeeklyReviewModel review, WeeklyReviewSections sections)
        {
            //先立旗，再落盘。
            //节拍判定只认这一个开关（需求 14），不立旗复盘主页会一直报「已逾期」；而正文里的
            //受管区块每次保存都重新生成，其中一行就是「本周周课已完成」——顺序反过来的话
            //那面旗是在正文写完之后才插上去的，这一行永远等不到。旗本身不影响散文是否落盘。
            review.IsFinalized = true;
            review.FinalizedTime = DateTime.Now;

            if (!await SaveAsync(review, sections).ConfigureAwait(false))
            {
                //存失败时 SaveAsync 根本没碰这一行，内存里也不该留一面没落库的旗，
                //否则页面上会显示成「已完成」而库里还是没有
                review.IsFinalized = false;
                review.FinalizedTime = null;
                return false;
            }

            //SaveAsync 有「什么都没写就提前返回」的路径，那种情况它不碰这一行，
            //所以这里再显式写一次这几个列（也顺带兜住「散文空、但旗要立」的情形）。
            review.UpdateTime = DateTime.Now;

            return await _weeklyReviewRepository
                .UpdateAsync(review, it => new
                {
                    it.FrequencyConfirmed,
                    it.GoalsConfirmed,
                    it.IsFinalized,
                    it.FinalizedTime,
                    it.UpdateTime,
                })
                .ConfigureAwait(false);
        }

        private Task<List<WeeklyGoalModel>> GetGoalsByReviewAsync(Guid weeklyReviewId)
        {
            //SqlSugar 的 OrderBy 是追加语义，这两句合成 ORDER BY Domain, CreateTime
            return _goalRepository.Queryable()
                .Where(it => it.WeeklyReviewId == weeklyReviewId)
                .OrderBy(it => it.Domain)
                .OrderBy(it => it.CreateTime)
                .ToListAsync();
        }

        private async Task<DiaryModel?> CreateDiaryAsync(WeeklyReviewModel review)
        {
            var created = new DiaryModel
            {
                Id = Guid.NewGuid(),
                Kind = ReviewKind.Weekly,
                //周课属于它结算的那一周，所以落在那一周的周日，而不是"今天"
                CreateTime = review.EndDate.Add(DateTime.Now.TimeOfDay),
                Tags = [],
                Resources = [],
            };

            return await _diaryService.AddAsync(created).ConfigureAwait(false) ? created : null;
        }

        private async Task<DiaryModel?> FindDiaryAsync(WeeklyReviewModel review)
        {
            if (review.DiaryId is Guid diaryId && diaryId != Guid.Empty)
            {
                var byId = await _diaryService.FindAsync(diaryId).ConfigureAwait(false);
                if (byId is not null)
                {
                    return byId;
                }
            }

            var start = review.StartDate.Date;
            var end = review.EndDate.Date.AddDays(1);

            return await _diaryService
                .FindAsync(it => it.Kind == ReviewKind.Weekly && it.CreateTime >= start && it.CreateTime < end)
                .ConfigureAwait(false);
        }
    }
}
