# 发现与决策

## 需求

- 课题记录要从「写一篇」变成「吸收与反哺」：看清反复出现的规律，并在与同一个人相处时能用上。
- 课题离开日记流，走独立结构化表；人物沿用 Person 标签。
- 日课 1.1 改成一行一件事、可挂人物标签，并在人物页呈现。

## 研究发现

### 课题现状（实施前）

- 课题是一篇普通 `DiaryModel`（`Kind` 为 null），正文是 4.1~4.8 的 Markdown，标签为 Person + Lesson。
  - `src/SwashbucklerDiary.Shared/Utilities/Review/LessonSections.cs:29-106`
  - `src/SwashbucklerDiary.Rcl/Pages/Review/LessonEditPage.razor.cs:492-552`
- 唯一写入点是 `LessonEditPage`（路由 `/lesson`），唯一入口是看板卡片
  `src/SwashbucklerDiary.Rcl/Pages/Review/KanbanPage.razor:92-115`。
- 没有课题列表页；能间接看到课题的只有通用标签页 `TagDetailsPage`。
- 系统里没有任何按 `TagKind.Lesson` 的查询、统计或回读。课题被当作普通日记混进首页流与搜索
  （`DiaryService.cs:64-67`）。
- `LessonType` 只在编辑页解析/渲染，没有按类型聚合、计数或排行。
- 除刚加的归档块外，课题不写状态记录、不写触因、不进周课/月课；归档块写入优点表时带
  `SourceDiaryId`，但全仓库没有读取处。
- 没有回访/复查/下一步跟踪字段或页面。

### 日课现状

- 日课正文六节由 `DailyReviewSections` 解析/渲染；1.1「生活上的」当前是一个散文字段
  （`DailyReviewSections.cs:46-49`）。
- 日课已有结构化副表 `DailyReviewModel`：`DiaryId`、`Date`、`Mood`、`AffirmationIds`、`IsCompleted`，
  注释明确「散文仍在正文里，这里只放需要计数/聚合的字段」。
- 1.4 微观失败已有「可重复分块 + 加一条」的正文格式先例
  （`DailyReviewSections.cs:123-151`）。
- 情绪芯片通过正文末尾的受管区块渲染，说明「副表字段 + 正文可读副本」有现成做法。

### 状态管理表先例

- `StateRecordModel` 是独立表，不写日记正文；状态表编辑页注释明确「这张表跟日课/周课/月课不是一条线」。
- 它的触因是 `TriggerTagId`，调用项是 `InvokedKind + InvokedId`。

### 导出与发布

- `ReviewExportService` 导出 `review.json`，包含 12 张复盘表，不含 `DiaryModel`。
- 单篇日记导出走 `DiaryFileManager`，JSON/MD/TXT/XLSX 都直接输出 `Content`。
- 本机只有 `maui-windows` 工作负载；发布需临时把 `TargetFrameworks` 限定为 Windows，发布后还原。
- 当前快捷方式指向 `src/SwashbucklerDiary.Maui/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/SwashbucklerDiary.Maui.exe`。

## 待复核的事实

- `TriggerService.GetWithCountsAsync` 的计数是否只来自状态记录；合并课题触因前要读实现。
- `ReviewExportService` 的 JSON 是否参与恢复/导入；若是，新增字段要兼容旧包。
- SqlSugar 写入能否把课题主表与类型关联表放进同一事务。
- 用户库中是否真的没有 Lesson 标签与旧课题日记。

## S3 复查结论（2026-11-14）

- 日课 1.1 的正文是受管区块，只能从日课表单编辑；通用日记编辑/阅读页不展示或不可改 1.1（D18）。
- 人物页「当前边界」取最近一条 Boundary 非空的课题（D19）。
- 回访到期日 = 课题发生日 + 14 天（D20）。
- 日课 1.1 一行可以没有人物标签；无标签行不上人物页（D21）。
- 新增风险：R14 受管区块在通用编辑页的隐藏；R15 补记旧课题导致回访立刻到期。

### 风险出口（S3 风险确认段）

- R1 解决 → W9；R2 解决 → W10；R3 解决 → W11；R4 解决 → W12
- R5 接收；R6 接收
- R7 解决 → W13；R8 解决 → W14
- R9 解决 → W15
- R10 接收；R11 接收
- R12 解决 → W16
- R13 接收
- R14 解决 → W17
- R15 接收

## W1 代码审查结论（2026-11-14）

- P0：无。
- P1：`DailyEventService.QueryByPersonAsync` 用 `DailyReviewId` 去重前直接 `ToDictionary`，同一日课多条事件会重复键。已改为先去重再查日期，并去掉重复的事件查询。
- P2：删除课题时归档行留下悬空 `SourceLessonId`。已在删除事务里清空三张库表的来源 Id。
- P3：课题类型显示顺序不稳定；已按枚举排序。
- P3：`FollowUpDueTime` 为空会被服务补成默认值；已在模型注释说明「取消回访用 Irrelevant」。
- 已验证：Rcl 构建 0 错误。

## W3 / W9 代码审查结论（2026-11-14）

- W3：无 P0/P1。P2 补了列表页的「＋」入口与事件摘要，列表不再只显示人物和日期。
- W9：无 P0/P1。P2 处理了事件为空时的搜索副标题，去掉多余分隔符。
- 已验证：Rcl 构建 0 错误。

## W4 / W13 代码审查结论（2026-11-14）

- W4：无 P0/P1。P2 避免了一次重复查询（待回访数从已加载列表算）；P2 给日课片段加可点跳转；P3 处理事件为空时的标题分隔符。
- W13：其他标签类型仍走原分支，未改动；构建通过。
- 已验证：Rcl 构建 0 错误。

## W5~W8 代码审查与验证结论（2026-11-14）

- W5：无 P0/P1。1.1 行可挂人物标签、旧段落兼容；通用编辑器仍能看到 1.1 正文，R14 只做到「保存时以副表为准」，留作后续。
- W6：月课 3.1 增加课题类型与人物候选；无 P0/P1。
- W7：`review.json` 纳入课题、类型关联、日课人物行；`LessonSections` 删除；无 P0/P1。
- W8：Rcl 构建 0 错误；Windows 发布成功；真库备份后首次启动，3 张新表与 3 个 `SourceLessonId` 列已建；库中 Lesson 标签 0 条。
- 未完成：交互冒烟（新建课题、归档、回访、人物页、日课 1.1、月课候选）需要用户手点。

## S6 收缩（2026-11-14）

- 删除 `ILessonService.GetLatestByPersonAsync`（无调用方）。
- 两个页面重复的「按名字取/建 Person 标签」抽到 `ITagService.GetOrCreatePersonAsync`。
- 保留：`UseTranAsync`（两个调用方）、三个页面的标题格式化（附加信息不同）。
- net：约 -30 行；Rcl 构建 0 错误。
- 发布产物停在收缩前；收缩只改内部调用，行为不变。最终发布需在用户关闭应用后重跑。

## 技术决策

| 决策 | 理由 |
|------|------|
| 课题独立表 | 回访、统计、月课候选都要求可查询字段 |
| 日课 1.1 用副表 + 正文管理区块 | 复用 `DailyReviewModel` / 情绪区块的既有做法 |
| 类型用关联表 | 多选与按类型计数都直接 |
| 人物沿用标签 | 复用现有 Person 标签与筛选 |

## 遇到的问题

| 问题 | 解决方案 |
|------|---------|
| 课题离开日记后失去日历/搜索/导出 | 用户接受；另建课题列表与导出口 |
| 归档来源 Id 无处可放 | 三张库表加 `SourceLessonId`；宏观目标 Note 无来源字段，列入风险 R11 |
| 发布需要全 TFM restore | 临时限定 Windows 目标框架，发布后还原 |

## 资源

- 计划：`.planning/2026-11-14-lesson-experience-loop/task_plan.md`
- 进度：`.planning/2026-11-14-lesson-experience-loop/progress.md`

## 视觉/浏览器发现

无。本任务未使用浏览器或截图。

---
*每执行 2 次查看/搜索操作后更新此文件*
