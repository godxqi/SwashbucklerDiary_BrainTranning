using SwashbucklerDiary.Rcl.Repository;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Services
{
    public class DailyReviewService : IDailyReviewService
    {
        private readonly IReviewRepository<DailyReviewModel> _dailyReviewRepository;

        private readonly IReviewRepository<SelfAffirmationModel> _affirmationRepository;

        private readonly IDiaryService _diaryService;

        private readonly IMediaResourceManager _mediaResourceManager;

        private readonly II18nService _i18n;

        private readonly IStumblingBlockService _stumblingBlockService;

        private readonly IReviewScheduleService _scheduleService;

        public DailyReviewService(IReviewRepository<DailyReviewModel> dailyReviewRepository,
            IReviewRepository<SelfAffirmationModel> affirmationRepository,
            IDiaryService diaryService,
            IMediaResourceManager mediaResourceManager,
            II18nService i18n,
            IStumblingBlockService stumblingBlockService,
            IReviewScheduleService scheduleService)
        {
            _dailyReviewRepository = dailyReviewRepository;
            _affirmationRepository = affirmationRepository;
            _diaryService = diaryService;
            _mediaResourceManager = mediaResourceManager;
            _i18n = i18n;
            _stumblingBlockService = stumblingBlockService;
            _scheduleService = scheduleService;
        }

        public async Task<DailyReviewModel?> GetByDateAsync(DateTime date)
        {
            var day = ReviewScheduleService.NormalizeDate(date);
            return await _dailyReviewRepository.Queryable()
                .Where(it => it.Date == day)
                .FirstAsync()
                .ConfigureAwait(false);
        }

        public Task<DiaryModel?> GetDiaryByDateAsync(DateTime date)
        {
            var day = ReviewScheduleService.NormalizeDate(date);
            return _diaryService.FindAsync(it => it.Kind == ReviewKind.Daily
                && it.CreateTime >= day
                && it.CreateTime < day.AddDays(1));
        }

        public async Task<bool> SaveAsync(DailyReviewModel review, DailyReviewSections sections)
        {
            var day = ReviewScheduleService.NormalizeDate(review.Date);
            review.Date = day;

            if (sections.IsEmpty && !HasAnyChip(review))
            {
                //空日课不落库：否则会凭空多出一条空日记，还把那天标成"已完成"。
                //但如果那天本来就写过日课，这次是把内容清空了，得把已经同步出去的失败行收回来。
                var existingDiary = await GetDiaryByDateAsync(day).ConfigureAwait(false);
                if (existingDiary is not null)
                {
                    await _stumblingBlockService
                        .SyncFromDailyAsync(existingDiary.Id, day, sections.MicroFailures)
                        .ConfigureAwait(false);
                }

                return true;
            }

            review.IsCompleted = true;

            var diary = await GetOrCreateDiaryAsync(day).ConfigureAwait(false);
            if (diary is null)
            {
                //日记没建起来就不该继续写 DiaryId，否则留下孤儿引用
                return false;
            }

            string content = sections.Compose(
                ReviewSectionText.DailyTitles(_i18n),
                ReviewSectionText.MicroFailureQuestions(_i18n));

            diary.Kind = ReviewKind.Daily;
            diary.Template = false;
            diary.Title = $"{_i18n.T("Daily review")} {day:yyyy/MM/dd}";
            diary.Content = ReviewContentBlock.Apply(content, await BuildFacetBlockAsync(review).ConfigureAwait(false));
            diary.Resources = _mediaResourceManager.GetDiaryResources(diary.Content);
            diary.UpdateTime = DateTime.Now;

            if (!await _diaryService.UpdateIncludesAsync(diary).ConfigureAwait(false))
            {
                return false;
            }

            review.DiaryId = diary.Id;

            var existing = await GetByDateAsync(day).ConfigureAwait(false);
            bool success;
            if (existing is null)
            {
                review.Id = Guid.NewGuid();
                success = await _dailyReviewRepository.InsertAsync(review).ConfigureAwait(false);
            }
            else
            {
                review.Id = existing.Id;
                review.CreateTime = existing.CreateTime;
                review.UpdateTime = DateTime.Now;
                success = await _dailyReviewRepository.UpdateAsync(review).ConfigureAwait(false);
            }

            if (!success)
            {
                return false;
            }

            //1.4 的每条失败同步成一行 StumblingBlockModel，供周课 2.1 数频率。
            //放在日课落库成功之后——日课没存上就不该留下半套数据。
            await _stumblingBlockService
                .SyncFromDailyAsync(diary.Id, day, sections.MicroFailures)
                .ConfigureAwait(false);

            return true;
        }

        public async Task<bool> DeleteAsync(DateTime date)
        {
            var day = ReviewScheduleService.NormalizeDate(date);

            var review = await GetByDateAsync(day).ConfigureAwait(false);
            var diary = await GetDiaryByDateAsync(day).ConfigureAwait(false);

            //失败行挂在日记 Id 上，而日记有两个可能的来源：
            //① 按日期找到的那篇；② 复盘行里记着的 DiaryId —— 后者才是权威关联。
            //两个都要清：日记可能在「库表」里被清空正文删掉了（WritePage 会这么干），
            //那时按日期找不到它，但失败行还挂着那个 Id，不清就是孤儿。
            var candidates = new[] { diary?.Id, review?.DiaryId }
                .Where(it => it is Guid id && id != Guid.Empty)
                .Select(it => it!.Value)
                .Distinct();

            foreach (var id in candidates)
            {
                //含已经判过的行：用户点的是「删除这次日课」，
                //判定失去了它指的那条描述也就没有留着的意义。
                //（这与「改日课时判过的行一律留着」是两条不同的路径，见 IStumblingBlockService。）
                await _stumblingBlockService.DeleteBySourceDiaryAsync(id).ConfigureAwait(false);
            }

            if (diary is not null && !await _diaryService.DeleteAsync(diary.Id).ConfigureAwait(false))
            {
                return false;
            }

            //后半段删不掉（比如并发删过）不算失败：那一天本来就没日课了，结果是对的
            return review is null
                || await _dailyReviewRepository.DeleteByIdAsync(review.Id).ConfigureAwait(false);
        }

        public async Task<List<DateTime>> GetReviewDatesAsync()
        {
            var dates = await _dailyReviewRepository.Queryable()
                .Where(it => it.IsCompleted)
                .Select(it => it.Date)
                .ToListAsync()
                .ConfigureAwait(false);

            return [.. dates.Select(it => it.Date)];
        }

        public async Task<DailyReviewWeekOverview> GetWeekOverviewAsync(DateTime date)
        {
            var week = _scheduleService.GetWeek(date);
            var start = week.Start.Date;
            //End 是周日零点；左闭右开区间要包含周日一整天
            var endExclusive = week.End.Date.AddDays(1);

            var reviews = await _dailyReviewRepository.Queryable()
                .Where(it => it.Date >= start && it.Date < endExclusive)
                .ToListAsync()
                .ConfigureAwait(false);

            var diaries = await _diaryService.QueryAsync(it => it.Kind == ReviewKind.Daily
                && it.CreateTime >= start
                && it.CreateTime < endExclusive)
                .ConfigureAwait(false);

            var reviewByDay = reviews
                .GroupBy(it => it.Date.Date)
                .ToDictionary(it => it.Key, it => it.First());

            var diaryByDay = diaries
                .GroupBy(it => it.CreateTime.Date)
                .ToDictionary(it => it.Key, it => it.First());

            var affirmationIds = reviews
                .SelectMany(it => it.GetAffirmationIds())
                .Distinct()
                .ToList();

            Dictionary<Guid, string> affirmationContents = [];
            if (affirmationIds.Count > 0)
            {
                var affirmations = await _affirmationRepository.Queryable()
                    .Where(it => affirmationIds.Contains(it.Id))
                    .ToListAsync()
                    .ConfigureAwait(false);

                affirmationContents = affirmations
                    .Where(it => !string.IsNullOrWhiteSpace(it.Content))
                    .ToDictionary(it => it.Id, it => it.Content!);
            }

            List<DailyReviewDayOverview> days = [];
            foreach (var day in reviewByDay.Keys.Union(diaryByDay.Keys).OrderBy(it => it))
            {
                reviewByDay.TryGetValue(day, out var review);
                diaryByDay.TryGetValue(day, out var diary);

                var sections = diary is null
                    ? new DailyReviewSections()
                    : DailyReviewSections.Parse(ReviewContentBlock.Strip(diary.Content));

                //短语按当天选择的先后顺序保留；库里已被删掉的 Id 跳过
                var affirmations = (review?.GetAffirmationIds() ?? [])
                    .Where(affirmationContents.ContainsKey)
                    .Select(it => affirmationContents[it])
                    .ToList();

                days.Add(new DailyReviewDayOverview(day, sections, review?.Mood, affirmations));
            }

            return new DailyReviewWeekOverview(start, week.End.Date, days);
        }

        /// <summary>除散文章节之外，芯片那几格也一个都没选。</summary>
        private static bool HasAnyChip(DailyReviewModel review)
        {
            return review.Mood is not null
                || review.GetAffirmationIds().Count > 0;
        }

        private async Task<DiaryModel?> GetOrCreateDiaryAsync(DateTime day)
        {
            var diary = await GetDiaryByDateAsync(day).ConfigureAwait(false);
            if (diary is not null)
            {
                return diary;
            }

            //自己生成 Id，不依赖 InsertAsync 回填，后面 review.DiaryId 才能立刻用
            var created = new DiaryModel
            {
                Id = Guid.NewGuid(),
                Kind = ReviewKind.Daily,
                CreateTime = day.Add(DateTime.Now.TimeOfDay),
                Tags = [],
                Resources = [],
            };

            return await _diaryService.AddAsync(created).ConfigureAwait(false) ? created : null;
        }

        /// <summary>
        /// 芯片的产物渲染成受管区块里的 Markdown。
        /// 它是「可读导出」的兜底：纯 Markdown 导出也能看出这次日课选了什么。
        /// 1.1 ~ 1.5 的内容不在这里——它们已经作为章节写进正文了。
        /// </summary>
        private async Task<string?> BuildFacetBlockAsync(DailyReviewModel review)
        {
            List<string> lines = [];

            if (review.Mood is MoodKind mood)
            {
                lines.Add($"- **{_i18n.T("How was today")}**: {_i18n.T(mood.ToString())}");
            }

            var affirmationIds = review.GetAffirmationIds();
            if (affirmationIds.Count > 0)
            {
                var affirmations = await _affirmationRepository.Queryable()
                    .Where(it => affirmationIds.Contains(it.Id))
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (affirmations.Count > 0)
                {
                    string joined = string.Join(" / ", affirmations.Select(it => $"「{it.Content}」"));
                    lines.Add($"- **{_i18n.T("Self affirmation")}**: {joined}");
                }
            }

            return lines.Count == 0 ? null : string.Join("\n", lines);
        }
    }
}
