# 进度日志

## 会话：2026-10-05

### 准备阶段：环境验证与需求确认（非实现阶段）
- **状态：** complete
- **开始时间：** 2026-10-05
- 执行的操作：
  - 检查本机环境：发现仅有 .NET 8.0.413、无 MAUI workload、无 Android SDK、JDK 为 1.8、VS 2022 为 17.9（过老）
  - `winget install Microsoft.DotNet.SDK.10` → **10.0.401**（成为默认版本）
  - `dotnet workload install maui` → **10.0.110**（下载约 2 GB，耗时 >10 分钟）
  - `winget install Microsoft.OpenJDK.17` → **17.0.20.1**（`JAVA_HOME` 已写入系统变量）
  - `-t:InstallAndroidDependencies` → Android SDK 装到 `C:\Users\GODXQI\AppData\Local\Android\Sdk`（platform-36 / build-tools 36.0.0 / cmdline-tools latest / platform-tools）
  - 构建并运行 Windows 版 → **成功**（进程存活，主窗口"侠客日记"）
  - 构建 Android Debug → **成功**，产出 `com.yucore.swashbucklerdiary.debug-Signed.apk`（37 MB，已签名可直装）
  - 与用户通过 `/grill-me` 确认 **18 条需求**（含 2 次被用户纠正问法后重来）
  - 启动架构 agent 完成代码库探索 + 实现方案设计
  - 用户选择 `/planning-with-files-zh` 建立持久化规划文件
  - **核对记录完整性**：发现 **7 处缺口** —— ①12 张表完整字段 ②13 个新页面清单 ③统计引擎 SQL ④完整风险表 ⑤验证步骤（含构建命令）⑥18 轮问答的选项与理由 ⑦用户两次纠正的教训
  - 已全部补入 `findings.md`（新增「设计方案详解」「风险与对策（完整 7 条）」「验证方式」「需求确认过程（18 轮问答）」四节）
  - 补记两条原先漏掉的决策到 `task_plan.md`（自由日记退居库表；关键设计写进 `.planning/` 而非 plan mode 临时文件）
  - **根因记录**：原设计只写进了 `~/.claude/plans/idempotent-soaring-pinwheel.md`，那是 plan mode 的临时产物、**不在持久注入范围**，将来可能被清理。`.planning/` 才是会被钩子反复注入的磁盘记忆 —— 关键设计必须落在这里
  - **按用户要求加入两道审查门禁**（已写入 `task_plan.md` 的「审查门禁」章节）：
    - 每个阶段（0~4）末尾 → 运行 `/code-review-expert` 检查需求实现与代码问题 → **修复 P0~P3 全部问题**
    - **新增阶段 5** → 对全部改动运行 `/ponytail-review` → 按审查意见修改
    - 每阶段完成判据升级为：**实现 done + 验证通过 + 代码审查通过 + 问题已修**
  - 确认 `/ponytail-review` 来源：已安装的 **ponytail 插件 v4.12.0**（`~/.claude/plugins/cache/ponytail/ponytail/4.12.0/.openclaw/skills/ponytail-review/SKILL.md`），可用
- 创建/修改的文件：
  - `.planning/2026-10-05-self-management-system/task_plan.md`（新建）
  - `.planning/2026-10-05-self-management-system/findings.md`（新建）
  - `.planning/2026-10-05-self-management-system/progress.md`（新建）
  - **项目源码：零改动**

### 阶段 0：地基（实现）
- **状态：** in_progress（实现完成 + 双平台构建通过 + 迁移实测通过；待代码审查门禁）
- 新增文件：
  - `Shared/Enum/`：`ReviewKind` `TagKind` `MoodKind` `GoalDomain` `GoalStatus` `BlockVerdict` `VirtueKind` `ArchiveKind` `ArchiveSource` `InvokedKind`（10 个）
  - `Shared/Models/`：`DailyReviewModel` `WeeklyReviewModel` `WeeklyGoalModel` `StumblingBlockModel` `MonthlyReviewModel` `LimitingBeliefModel` `EnemyModel` `VirtueEntryModel` `SelfAffirmationModel` `ArchiveItemModel` `MacroGoalModel` `StateRecordModel`（12 张表）
  - `Rcl/Repository/ReviewRepository/`：`IReviewRepository<T>` + `ReviewRepository<T>`（通用仓储，替代 24 份样板）
  - `Rcl/Pages/Review/KanbanPage.razor`（`/`）、`Rcl/Pages/Statistics/StatisticsPage.razor`（`/statistics`）
- 修改文件：
  - `DiaryModel` += `Kind`（`ReviewKind?`）、`TagModel` += `Kind`（`TagKind?`）—— **均可空**，理由见 findings
  - `AddSqlsugar.cs`：`types[]` += 12 张表；新增 17 个索引的 `IndexGroupNameList` 配置
  - `MainLayoutBase.cs`：`navigationButtons` → 今日 / 库表 / 统计 / 设置
  - `HomePage.razor`：`@page "/"` → `@page "/library"`
  - 4 个平台的 `AddDependencyInjection.cs`：开放泛型注册 `IReviewRepository<>`（Maui/Gtk/WASM 用 Singleton，Server 用 Scoped，与各自既有仓储注册的生命周期一致）
  - 12 个 i18n 语言包：补 `Today` `Library` `Review kanban is coming soon` `Statistics is coming soon`
- **踩到的坑（已修）**：`EntityColumnInfo` 上**没有** `UniqueGroupNameList`（编译报错 CS1061）。DLL 里那个字符串属于别的类型。改用 `IndexGroupNameList`，唯一性交给服务层按业务键 upsert。
- **安全措施**：改前对 9 个待改文件做了 `.backup.claude` 备份；跑迁移实测前备份了整个 AppData `Data` 目录
- 用户手测未完项：桌面端点击 4 个 tab 切换（见 findings「未能自动验证的一项」）

### 阶段 1：MVP（看板 + 日课 + 肯定库 + 节拍）
- **状态：** in_progress（实现 ✅ / 构建 ✅ / 看板实测 ✅ / 待审查门禁 + 人工手测）
- 新增文件（14 个）：
  - `Shared/Utilities/Review/ReviewContentBlock.cs`（受管区块：剥离 / 写入，幂等）
  - `Rcl/Services/ReviewSchedule/`：`IReviewScheduleService` + `ReviewScheduleService` + `ReviewTask`/`ReviewTaskState`
  - `Rcl/Services/Data/AffirmationService/`、`DailyReviewService/`、`ReviewTemplateService/`
  - `Rcl/Pages/Review/KanbanPage.razor(.cs/.css)`、`DailyReviewPage.razor(.cs/.css)`
  - `wwwroot/json/icon/emotion-icons.json`
- 修改文件：`Setting`（9 项）、`IGlobalConfiguration(+Extension)`、4 平台 DI、12 个 i18n 语言包
- **实测抓到 1 个真 bug 并修掉**：周课锚点偏移一天（`DayOfWeek.Friday=5` 是周日基准，周起点却是周一）
  → 修法 `(v + 6) % 7`；修前看板显示「2026-09-28 - 10-05」，修后正确显示**已逾期**
- **关键设计改动**：`MarkdownEdit` 没有整段设值 API → 受管区块改为「加载剥离、保存生成」
- **走过的弯路（勿重复）**：曾用 Server 版预渲染验证页面，结果发现**预渲染不实例化页面组件**
  （对照 `/library`、`/statistics`、`/mine` 同样只有导航栏，且无异常日志）→ 此路不通
- 构建：Windows / Android / Gtk / Server 全部 0 错误；WebAssembly 仍缺 `wasm-tools` 工作量
- **代码审查门禁已跑并修完 P0~P3 共 9 项**（明细见 `task_plan.md`）：
  最重要的一条是 **P1 数据丢失** —— 用返回键离开日课页时不保存，
  已照 `WritePage` 注册 `NavigateController.AddHistoryAction` 解决
- 再构建：Windows / Android 均 0 错误；看板复测正常
- 待办：**主人手测日课闭环**（步骤见 `task_plan.md`「下一阶段」）

### 阶段 1：主人验收后的三轮追加改动（2026-10-05）
主人跑通日课闭环后，连着提了三批修改，都已实现 + 构建通过：

**第 1 轮 —— 顺序与说明**
- 日课页**按文档 1.1~1.7 重排**：七节各一个独立字段，一节不缺（主人指出旧版缺章节、顺序也不对）
- 删掉「类别」（文档把归类写在周课 2.1），`DailyReviewModel.Category` 字段删除
- 新增 `DailyReviewSections`（正文 ↔ 字段，按章节号锚定，与语言无关）与 `ReviewSectionText`（七节说明 + 1.4 四问题干）
- 选中芯片改**反色**（项目没有 `--m-theme-primary`，只有 `--m-theme-on-surface`/`--m-theme-surface`）+ 加 `Filter="true"`
- 选中情绪后把该节说明换成**该情绪本身的意思**（八条描述补进 i18n）
- **日课模板系统整体删除**（`IReviewTemplateService` + 4 平台 DI + 3 个设置项）
- 新增 `DailyReviewSections`，同时删了 `DailyReviewModel.MicroFailure`（单字符串表达不了 N 条）

**第 2 轮 —— 固定条数分框 + 每节说明**
- 每节标题下显示**「这要填什么」**（取自方法文档对应章节）
- **1.3 三个框**；**1.4 一组四问四个框**（每格回显题干）

**第 3 轮 —— 1.4 可重复 + 1.7 可删**
- **1.4 的「一组四问」可增删**（四问是针对**一个**微观失败的，一天可能不止一条）：
  `MicroFailures` 改为 `List<string[]>`，正文里每条失败一个 `### 编号` 分块
- **1.7 的肯定芯片挂了 ✕**（MASA `MChip.Close` + `OnCloseClick`），走现成 `DeleteDialog` 确认；
  若当天选过它，同步从选中集合里摘掉，避免往受控区块写已不存在的 Id

- 构建：Windows / Android / Gtk / Server 均 0 错误
- **规划文件已按这三轮改动全面校准**（含修正「下一步」「当前阶段」「关键问题 1 已解决」「阶段 2 口径变更」等 9 处过期内容）

### 阶段 0：代码审查门禁 + 修复
- 运行 `/code-review-expert`，再构建一次（Windows 0 错误）
- 修完 P0~P3 共 6 项（明细见 `task_plan.md` 的「阶段 0 代码审查门禁结果」）
  - P0：日历/文件失去入口 → 在「库表」tab 顶部加入口区
  - P0：`*.backup.claude` 会被提交 → 加进 `.gitignore`
  - P1：`Kind` 的 null 语义无收口 → 新增 `Shared/Extensions/ReviewModelExtensions.cs`
  - P2：索引组名不控制索引名 → 改注释；`AffirmationIds` JSON 序列化收口
  - P3：`MoodKind` 三个落点的同名歧义 → 写明注释
- 按用户当场决定改了两处 UI：第 4 个 tab 改成「设置」+ 齿轮图标 → `/mine`；日历/文件入口放库表页顶部
- **GUI 自动化验证失败并查明原因**：Windows 前台锁使后台进程合成的鼠标事件无法送达 WebView2；
  `swashbucklerdiary://` 协议在非打包 Windows 版**未注册**，deep link 冷启动也不可用。
  （之前的点击失败还叠加了一个坐标错误：窗口实际在 (101,101)，我按 (25,25) 算的坐标。）
  → 这一项只能人工手测

### 阶段 2：周课 + 目标卡 + 触因（2026-10-05）

- **状态：** in_progress（实现 ✅ / 四平台构建 ✅ / 真机运行期验证 ✅ / 待代码审查门禁 + 主人手测）
- 开工前先做了「阶段 1 末轮改动对本阶段的影响」评估，7 条写入 `task_plan.md`；其中第 ① 条
  （`StumblingBlockModel` 缺稳定序号）是本阶段唯一的模型改动
- 新增文件：
  - `Shared/Utilities/Review/ReviewSections.cs`（章节切分/拼装的公共部分，日课也改用它）
  - `Shared/Utilities/Review/WeeklyReviewSections.cs`（2.4 追问 / 2.5 四处出口 / 2.6 新事物）
  - `Shared/Models/StumblingBlockModel` += `OrderIndex`；`WeeklyGoalModel` += `SettlementNote`
  - `Rcl/Services/Data/`：`StumblingBlockService` / `WeeklyReviewService` / `ArchiveService` /
    `VirtueService` / `TriggerService` / `MacroGoalService`
  - `Rcl/Pages/Review/`：`WeeklyReviewPage` / `WeeklyGoalEditDialog` / `GoalSettleDialog` /
    `TriggerPage` / `MacroGoalPage` / `MacroGoalEditDialog`
- 修改文件：`DailyReviewService`（保存后同步失败行）、`KanbanPage`（周课入口 + 触因提醒卡）、
  `HomePage`（库表加入「触因 / 宏观目标」入口）、`ReviewSectionText`（周课文案）、
  `AddSqlsugar`（联合索引 `(SourceDiaryId, OrderIndex)`）、4 平台 DI、`app.css` + 日课页
  （把 `daily-review-*` 样式提为全局 `review-*`，日课/周课/对话框共用一套观感）
- i18n：zh-CN / en-US 各补 **64 条**（脚本写入，已校验未丢键、未改值、CRLF 未变）
- **踩到的坑（已记录，勿重复）**：
  1. **把 `BlazorWebView.StartPath` 指到非根路由会让整个界面空白**——
     不是周课页的问题，是启动路径的用法错误。验证页面渲染必须走**应用内跳转**。
  2. 真机截图/合成点击在 3840×2160 @200% 缩放的桌面上坐标是**虚拟化**的，
     `GetWindowRect` 与 `SetCursorPos` 一致即可用，但**合成点击不可靠**（试了 3 种写法），
     改用「应用内自动跳转 + 截图」才稳定复现。
- **真机运行期验证（已实测，非推断）**：
  | 验的是什么 | 结果 |
  |---|---|
  | 补列迁移：`OrderIndex` / `SettlementNote` | ✅ 真机库补列成功，联合索引 `Index_StumblingBlockModel_SourceDiaryId_OrderIndex` 已生成 |
  | 可空枚举查 SQL `it.Kind == TagKind.Trigger` | ✅ 翻译正确：只查到显式标了 Trigger 的标签，**老标签（NULL）没被误当触因** |
  | `GroupBy(TriggerTagId) + AggregateCount` | ✅ 计数正确：4 次的进提醒卡，1 次的不进 |
  | 看板触因提醒卡 | ✅ 「被否定 4 × · 交给月课 3.1」 |
  | 触因页 `/triggers` | ✅ 列表与文案正常 |
  | 周课页 2.1（类别汇总 + 类别芯片 + 判定芯片） | ✅ 灌 6 条数据后显示「刷手机 ×3 拖延 ×2 未归类 ×1」，选中态反色正常 |
  | 周课页 2.2 / 2.3 / 2.4 骨架与文案 | ✅ 空态与说明都正确 |
- **仍未验证（留给主人手测）**：2.3 目标卡列表与编辑对话框、2.4 结算芯片与原因对话框、
  2.5 / 2.6 文本框、每日「日课保存 → 失败行落库」的在线同步链路（逻辑已按影响①设计，但没跑过端到端）

### 阶段 2：代码审查门禁 + 修复（2026-10-05）

- 运行 `/code-review-expert`，**P1×2 + P2×2 + P3×5 全部修完**（明细见 `task_plan.md` 的
  「阶段 2 代码审查门禁结果」）
- 最重要的一条：**`IsFinalized` 全仓库只读不写** → 周课永远显示「已逾期」，
  需求 14 的节拍完成度完全失真。修法是加 `FinalizeAsync` + 页面底部「完成本周周课」按钮
- 第二条 P1：**隐私切换对复盘日记没设防** —— 计划里早就写下的对策（「抄复盘的日记禁止隐私切换」）
  一直没落地。修法是给菜单项加 `IsPrivacySwitchable`（= 自由日记才允许），并在动作里再挡一道
- 再构建：Windows / Android / Gtk / Server 全 0 错误
- **真机实测「已完成」旗子的读取链路**：给 2026-W40 塞一条 `IsFinalized=1` →
  看板周课由「已逾期」变「已完成」；测完把种子数据清掉

### 阶段 2：主人手测反馈 —— 2.2 要看得见候选池（2026-10-05）

- 主人指出：**2.2 应该显示现在候选池有哪些**
- 原来的写法是横排芯片，`review-chip-group` 带 `white-space: nowrap` → 描述一长就被截到屏幕外，
  等于看不见池里有什么；而且**候选池的定义也只认「人工判成绊脚石」**，和文档 2.1 的
  「同类 ≥2 次才是绊脚石」对不上
- 改成：
  - 候选池 = 「**同类 ≥2 次**」（按文档规则自动进池）**或**「人工判成绊脚石」（人的判断优先）
  - **竖排清单**，每条显示 `类别 ×次数`（人工单独标的补一句「人工判定」），长描述会换行不再被截
  - 点整行选中 / 再点取消，选中态整块加深 + 左侧竖条变实（沿用芯片那套反色思路）
  - 标题行给出 `候选池 · N`
- 真机实测（灌 6 条：刷手机×3 / 拖延×2 / 未归类×1）：候选池显示 `候选池 · 5`，
  刷手机 ×3 三条 + 拖延 ×2 两条，**未归类那 1 条按规则（1 次不算绊脚石）没进池** ✅
- 再构建：Windows / Android / Gtk / Server 全 0 错误；临时诊断代码与演示数据已清干净

### 阶段 2：收尾（主人手测通过）

- **状态：** ✅ complete（2026-10-05 主人确认手测通过）
- 阶段 2 的全部判据达成：实现 ✅ / 四平台构建 ✅ / 真机运行期验证 ✅ / 代码审查 P1~P3 全修 ✅ / 主人手测 ✅

### 阶段 3：开工准备（2026-10-05）

- **状态：** in_progress
- 读完了方法文档 `F:\日记\自我管理系统-最终版.md` 的第 3 / 4 / 5 / 6 / 7 节（月课四步、
  自我肯定库、状态管理表、三个优点表、四个 List）与第 8 节回环总图，据此写了
  「**阶段 3 开工前的回顾**」8 条到 `task_plan.md`（这 8 条是动手前的必读）
- 摸清了家底（**结论：阶段 3 不缺表，缺服务与页面**）：
  - 12 张表 + 4 个索引在阶段 0 就建好了（`MonthlyReviewModel` / `LimitingBeliefModel` /
    `EnemyModel` / `VirtueEntryModel` / `ArchiveItemModel` 全在 `AddSqlsugar` 的 `types[]` 里）
  - 已有服务：`ReviewSchedule`（**已在读月课的 `IsFinalized`，但无人写**）/ `DailyReview` /
    `Affirmation` / `StumblingBlock` / `WeeklyReview` / `Archive` / `Virtue` / `Trigger` / `MacroGoal`
  - 已有页面：看板 / 日课 / 周课 / 触因 / 宏观目标 + 两个目标对话框
  - **缺**：月课服务与页、优点表页、归档区页、肯定库页
- 从代码里核实的三处关键事实（省掉了重新踩坑）：
  1. `ArchiveService.GetFeedbackCandidatesAsync` 已写好但**没有调用方**——回流读取侧留的接口
  2. `StumblingBlockService` **已经依赖** `IArchiveService` → 回流不能反向注入（会成环），
     只能在 `ArchiveService` 里直接插 `StumblingBlockModel`
  3. `SelfAffirmationModel` 只有自由文本 `Source`，**查不出「本次月课产的肯定」** →
     3.2 的闸门需要补一列 `SourceMonthlyReviewId`（这是阶段 3 唯一的模型改动）

### 阶段 3：实现 + 四平台构建 + 真机验证（2026-10-05）

- **状态：** 实现 ✅ / 四平台构建 ✅ / 真机运行期验证 ✅ / 待代码审查门禁 + 主人手测
- 新增文件（16 个）：
  - `Shared/Utilities/Review/MonthlyReviewSections.cs`（月课摘要的**单向渲染** + `MonthlyReviewLabels`）
  - `Rcl/Services/Data/MonthlyReviewService/`（`IMonthlyReviewService` + 实现，含四步闸门）
  - `Rcl/Pages/Review/`：`MonthlyReviewPage`（四步向导）/ `LimitingBeliefEditDialog` /
    `EnemyEditDialog` / `AffirmationEditDialog` / `VirtuePage` / `AffirmationLibraryPage` / `ArchivePage`
- 修改文件：`SelfAffirmationModel`（+`SourceMonthlyReviewId`）、`ReviewSectionText`（+月课文案与栏目标签）、
  `StumblingBlockService`（+`GetByMonthAsync`，抽 `ToViewsAsync`）、`ArchiveService`（+`FeedBackAsync`/`DeleteAsync`）、
  `VirtueService`（+`DeleteAsync`/`UpdateAsync`/`CountAsync`/`GetAchievementGrowthAsync`）、
  `AffirmationService`（+`GetByMonthlyReviewAsync`）、`WeeklyReviewService`（+`GetCarriedOverGoalsAsync`）、
  `KanbanPage`（月课入口 + 7.4 回流卡）、`HomePage`（归档区/肯定库/三个优点表入口）、
  `app.css`（步骤芯片样式）、4 平台 DI、`zh-CN`/`en-US` 各补 **96 条**
- **真机运行期验证（都是实测，不是推断）**：
  | 验的是什么 | 结果 |
  |---|---|
  | 补列迁移 `SelfAffirmationModel.SourceMonthlyReviewId` | ✅ 以可空 `uniqueidentifier` 追加成功；**从备份库重跑一次迁移也成功**（可重复执行） |
  | `GetByMonthAsync`（按 ISO 周圈整月） | ✅ 灌 6 条失败后，月课 3.1 的素材正确显示「刷手机 ×3 / 拖延 ×2」，1 次的那条按门槛没进池 |
  | 3.1 的素材来自触因 | ✅ 「被否定 ×3」（3 条状态记录）出现在素材里 |
  | 月课四步闸门 | ✅ 1/2/3 达标后第 4 步可进入、步骤芯片显示「已完成」；`gates.AllDone` 时「完成本月月课」按钮才可点 |
  | **7.4 回流（写路径）** | ✅ `FeedBackAsync` 真机跑通：归档条打了 `FedBackYear=2026 / W41`，并**在本周新建了一条 `Verdict=0`、`SourceDiaryId=NULL`、`OrderIndex=0` 的失败行** —— 它正好回到周课 2.1 的清单里 |
  | **完成月课 → 写回日记** | ✅ 生成日记 `月课 2026/10`、`Kind=3`、`CreateTime=2026-10-31`（月末而不是今天）；正文 449 字符，受管区块内是 `## 3.1`~`## 3.4` 的生成式摘要（3.4 渲染成 Markdown 表格）；四个闸门标记与 `IsFinalized` 全部落库 |
  | 看板月课入口 | ✅ 月课那行显示「已逾期」（不再是弹「功能不可用」），点它进月课页 |
  | 看板 7.4 回流卡 | ✅ 出现「该回流的微小失败」卡，列出累计 3 次的那条 |
  | 成就增长 | ✅ 成就列表页顶部显示「成就增长 · 2 · 2026/10 +1」（跨月聚合正确） |
- **真机抓到的两个问题（都不是应用逻辑的错，但都值得记）**：
  1. **灌种子数据时 Guid 少写了两位**（`PREFIX + "6%02d"` 得到 13 位末段）→
     SqlSugar 读 `StumblingBlockModel` 直接报 `Id bind error`。教训：往真机库灌数据时，
     Guid 末段必须凑满 12 位；这同时也验证了**读写链路会把坏数据暴露成明确异常而不是静默错值**。
  2. **月课步骤条自搭的那版 flex 宽度算飞了**（真机量出来每段 926px、整行 3734px 溢出 2452px 的容器），
     只剩 3 段可见。改用项目里已经跑通的芯片写法（`review-chip-group` 自带 `overflow-x: auto`）后正常，
     而且顺带能显示步骤名。
- **i18n 的两个坑（都是真机截图才暴露的）**：
  - `I18n.T(x switch { … => "字面量" })` 里的 key **不在 `I18n.T(` 正后面**，第一版扫描脚本漏了 → 归档区芯片显示英文
  - 服务里的参数名是小写 `i18n`，`"I18n.T("` 区分大小写又漏了一批 → 3.2 的标题在正文里显示成英文
  - 两次都补了；最终 133 个 key 全部命中，`zh-CN` 676→772、`en-US` 663→759，**零丢失零改动**
- **临时诊断代码已全部删除**（`KanbanPage` 的自动跳转与写路径演练、月课页的默认步骤），
  删后四平台重新构建 **0 错误**
- **真机库已还原**：用测试前的 `Data.backup.claude-stage3` 覆盖回来，再启动一次让迁移重跑，
  确认 **5 篇主人日记完好、零测试残留**（`5eed` 前缀 0 条、月课日记 0 篇、失败行 0 条）

### 阶段 3：代码审查门禁 + 修复（2026-10-05）

- 运行 `/code-review-expert`：**P1×1 + P2×3 + P3×9 全部修完**（明细见 `task_plan.md` 的
  「阶段 3 代码审查门禁结果」）
- 最重的一条是 **P1 需求偏差**：文档 7.4 的出口写着「累计 3 次 → **回到 2.1 重新进入候选池**」，
  而候选池只认「同类 ≥2 次」或「人工判成绊脚石」——回流行两条都不满足，于是**看得见、进不去**。
  修法是给 `IArchiveService` 加 `GetRecurringContentsAsync`（7.4 里累计 ≥3 次的内容），
  候选池补上这条依据，标签注明「来自 7.4 的回流」
- **这条 P1 的修复也在真机验过**：灌一条累计 3 次的 7.4 条目 + 它回流后的那一行 →
  周课 2.2 的候选池显示 `候选池 · 1`：「未归类 · 来自 7.4 的回流 / 答应了不该答应的活，结果自己熬夜」；
  只出现 1 次的对照条（拖延 ×1）**没进池**。新查询也没有 SqlSugar 翻译报错（日志零新增错误）
- 另外 12 条小问题也都修了：优点表编辑框空白（`InputDialog` 的 `Text` 预填）、
  下周目标卡改成**保存时**才建下周周课、`OccurrenceCount` 只在 7.4 累加、
  回流行排序补 `CreateTime` 兜底、空月课行不再建、肯定库补「启用」开关、
  `?kind=` 切栏同步地址栏、`StepCount` 引用 `MonthlyReviewSections.MaxSection`、
  `FeedBackAsync` 校验 Kind、删掉没人用的 `CountAsync`
- 修完再跑四平台构建：Windows / Android / Gtk / Server **全 0 错误**；
  临时诊断代码全部删除后重新构建同样 0 错误
- **真机库最终状态确认**：还原到测试前的备份 → 启动一次让迁移重跑 →
  `SourceMonthlyReviewId` 在列里 ✅、**5 篇主人日记完好** ✅、复盘八张表**全部 0 行**（零测试残留）✅
- **验证截图已删**（验证用的临时产物，约 2.5 MB，`shots/` 整个删掉）；
  `scripts/` 下那几个核对脚本留着，阶段 4 还要用（见 `task_plan.md` 文末备注）

### 阶段 4：实现 + 四平台构建 + 真机验证（2026-10-05）

- **状态：** 实现 ✅ / 四平台构建 ✅ / 真机运行期验证 ✅ / 待代码审查门禁 + 主人手测
- **主人拍板两处**：
  1. 阶段 3 的 13 项手测**跳过**，直接进阶段 4（并入阶段 5 的全链路回归）
  2. i18n **只补 zh-CN / en-US**，其余 11 个语言包保持现状（详见 `task_plan.md` 的说明框）
- 新增文件（9 个）：
  - `Rcl/Services/Data/StateRecordService/`（`IStateRecordService` + 实现：列表 / 按日 / 取一条 / 存 / 删 / 计数 + 跨表引用批量解析）
  - `Rcl/Services/Data/ReviewStatsService/`（`IReviewStatsService` + 实现：三个完成度 + 失败排行）
  - `Rcl/Services/Data/ReviewExportService/`（`IReviewExportService` + 实现：12 张表导出 / 主键 upsert 导入）
  - `Rcl/Services/DiaryFileManager/ReviewExportModel.cs`（`review.json` 的容器）
  - `Rcl/Pages/Review/StateRecordEditPage.razor(.cs)`（`/stateRecord` 完整表单）
  - `Rcl/Pages/Review/StateRecordListPage.razor(.cs)`（`/stateRecords` 历史列表）
  - `Rcl/Pages/Review/InvokedPickerDialog.razor(.cs)`（「现在要调用哪一条」选择器）
  - `Rcl/Pages/Statistics/StatsBarRow.razor` + 重写 `StatisticsPage.razor(.cs)`
- 修改文件：`DiaryFileManager`（4 平台子类 + 基类：写/读 `review.json`，导入时**排除**它）、
  `WeeklyReviewService`（+周课 facet 区块；`FinalizeAsync` 改成**先立旗再落盘**，否则「本周已完成」那行永远等不到）、
  `ReviewSectionText`（+`DomainText/StatusText/VerdictText`，收掉原先 4 份重复；+节拍说明文案）、
  `TriggerService`（删掉私有 `HasNothingButTrigger`，改用 `ReviewModelExtensions.IsTriggerOnly`）、
  `WeeklyReviewPage` / `WeeklyGoalEditDialog` / `MacroGoalEditDialog` / `MacroGoalPage`（三个文案方法改为委托）、
  `SettingPage`（+复盘节拍组）、`HomePage`（+状态管理表入口）、`KanbanPage`（+「记一笔现在的状态」卡）、
  `app.css`（+统计页条形样式）、4 平台 DI、`zh-CN` / `en-US` 语言包

- **真机运行期验证（都是实测，不是推断）**。用一段**临时诊断代码**把三条链路在真机跑了一遍，
  结果写进 `%TEMP%\stage4-diag.txt`（验证完已整个删除，`grep` 确认零残留）：

  | 验的是什么 | 结果 |
  |---|---|
  | **统计聚合（风险最高的一条）** | ✅ `completion daily=0/90 weekly=0/13 monthly=1/3` —— 月课分母 3 = 90 天窗口里已经走完的 7/8/9 三个月，分子 1 = 主人自己手测月课留下的 `IsFinalized`。失败排行那条 `Year*100+WeekOfYear` 的跨年区间查询**SqlSugar 翻译通过、没报错** |
  | 导出 | ✅ `daily=1 monthly=1 beliefs=1 enemies=1 affirmations=1`，与库里逐项对得上 |
  | 导入幂等 | ✅ 把刚导出的原样导回去，`handled=5`，之后四张表条数**一条不差**（只动了 `UpdateTime`） |
  | 状态记录闭环 | ✅ 建（返回 Id）→ 读回（触因名与「调用哪一条」都解析出正文）→ 计数 → 删 → 归零 |
  | 空表单 | ✅ 被拒（返回 null） |
  | **节拍设置** | ✅ 写 `WeeklyReviewAnchorDay=1` → 读回 1 → **判定跟着变**：锚定日 10-02 宽容至 10-04「已逾期」变成 10-05 宽容至 10-07「待办」→ 已还原为 5 |
  | 界面 | ✅ 逐页截图：统计页（条形宽度换算后**正好 33%**、数值居中在条里）、状态管理表表单、设置页复盘节拍组（默认周五 / 2 / 3 都对）、看板「记一笔现在的状态」卡 |
  | 四平台构建 | ✅ Windows / Android / Gtk / Server **全 0 错误** |
  | 真机库 | ✅ 测试前备份、测试后还原；6 篇日记、月课行内容与 `DiaryId` 全部未变，零测试残留 |

- **踩到的坑（已修，教训记 findings）**：
  `StatsBarRow` 第一版自己搭了「flex 标题行 + div 轨道」，真机上**整行宽度算飞**，
  数值被挤到屏幕外看不见 —— 和阶段 3 那次「自搭 flex 步进条」是同一类错误。
  改用项目里已经跑通的 `MProgressLinear`（成就卡就在用，数值印在条里）之后正常。
- **截图工具本身也有个坑（已修 `snap-app.ps1`）**：PowerShell 是 DPI-unaware，
  只截得到物理屏幕的 1/1.5；**最大化**会让窗口比能截到的范围宽，截图里只剩窗口左边 2/3，
  右边内容全丢（连阶段 1 就验过的 `/library` 也一样丢，一开始差点误判成我的布局 bug）。
  改成一个确定装得下的窗口尺寸（1300×900）整窗才都在画面里。

### 阶段 4：代码审查门禁 + 修复（2026-10-05）

- 运行 `/code-review-expert`：**无 P0 / 无 P1；P2×3 + P3×7 全部处理**（明细见 `task_plan.md`）
- 其中 **两条有意不改，且理由写进了代码注释**（这是这次审查里最值得记的部分）：
  1. **不用「日记导入成功」当复盘数据导入的闸门** —— 审查的本意是防孤儿引用，
     但日记导入返回 `false` 的常见情形之一是「这些日记库里已经有了、什么都没变」，
     加了闸门会让**「再恢复一次同一个备份、复盘数据静默没回来」**。反过来日记真失败时，
     复盘行引用的日记会在下次成功导入后补上，是可自愈的。两害相权取下者。
     （另一半建议保留了：`diaries` 为空时不再调 `ImportAsync([])`。）
  2. **`review.json` 装全量** —— 任何「按子集挑着带」的规则都会踩到 `SourceDiaryId` 为空的
     回流行和手动归档，那是真丢数据。改为在 `ReviewExportModel` 上写明取舍 + 记进阶段 5 待办
     （在导出界面给用户一句提示）。
- 其余 8 条都改了：失败排行上界多算一周、`MergeAsync` 返回值不诚实、
  `periodKeys` 每次渲染重新分配、列表页触因名重复显示、`CountAsync` 死方法等
- **修复后重新真机复验**：`export-json` 包里 `zip-entries=review.json,version.json`
  （`review.json` 2253 字节）、**重复导入同一个包仍 `ok=True`**、导入前后四张表条数一条不差、
  修正后的失败排行查询无 SQL 报错 → `RESULT=OK`
- 删掉临时诊断后重跑四平台构建：Windows / Android / Gtk / Server **全 0 错误**；
  真机库还原后 6 篇日记 + 月课行内容完好、零测试残留

### 阶段 5：`/ponytail-review` + 修改（2026-10-05）

- **状态：** ✅ complete（实现 / 四平台构建 / 真机复验全过；两处待主人拍板见 `task_plan.md` 的「下一步」）
- `/ponytail-review` 本会话**没有注册成 Skill**，但插件目录里有它的定义，照规格手工执行：
  **只看过度设计**，标签 `delete` / `stdlib` / `native` / `yagni` / `shrink` / `reuse`
- 用三个并行 agent 分头猎捕（死代码 / 重复实现 / 过度抽象与啰嗦），共报 ~40 条候选，**逐条自己核实**
- **采纳并施工 20 余条，净减 290 行**（新增源码 10247 → 9957）。三条最有价值的都是 `reuse`：
  1. **7 个复盘页手写的「返回钮 + 标题」换回 `MyAppBar Title=…`** —— 该组件本来就支持，项目里已有
     10 个页面这么用；我们却在 7 个新页面里把它抄了一遍（~77 行）
  2. **`ParseItems`/`Join`/`ItemRegex`/`StripLabel` 在日课与周课里逐字复制**（周课还得写成
     `DailyReviewSections.ItemRegex` 去引用它——本身就是分层的味道）→ 全部收进 `ReviewSections`（~134 行）
  3. **`ChipClass(bool)` 在 10 个文件里各写一遍** → 挪到共同基类 `MyComponentBase`（~36 行）
  另外还删了：两个从没被写过的 `ArchiveSource` 取值、没人写的 `TagKind.Category`、
  可从别处推导的 `IsAchieved`/`AchievedTime`、只写不读的 `LastRepeatedTime`、
  零调用的 `QueryByDateAsync`、没有消费方的 `Task<int>` 返回值、
  接口上只被内部调用的 5 个成员、填了没人读的 `ReviewTask.AnchorDate`
- **三条「新建抽象」式的复用有意不做**（抽对话框壳 / 列表页壳 / 芯片组，~330 行）：
  它们是新造组件而不是复用现成的，且要重新逐屏校验主人刚手测过的 13 个界面 —— 留给主人拍板
- **两处 `delete` 也有意不做并写明理由**：`VirtueEntryModel.SourceDiaryId` 与
  `WeeklyGoalModel.SettledWeekOfYear` 都在**已批准的规划数据模型表**里，删它们是改需求不是砍过度设计
- **削减之后真机复验**（临时诊断，验完已删、`grep` 确认零残留）：
  | 验的是什么 | 结果 |
  |---|---|
  | 正文解析器（`ParseItems` 搬家的风险点） | ✅ 拿**真库里的日课正文**跑：`micro=1 positives=3`、`mf[0]=[1\|2\|\|]` 与库里逐字一致；解析→拼回→再解析往返稳定 |
  | 周课 2.5 四处出口 | ✅ `exits=4`，多行续接（「出口二\n续行并进来」）正确合并 |
  | 导出导入（删字段后） | ✅ 前后计数一条不差 |
  | 统计聚合（循环合并后） | ✅ `0/90 · 0/13 · 1/3`，与削减前完全一致 |
  | 状态记录（判据收口后） | ✅ 存→读→空表单被拒→删→归零 |
  | 界面 | ✅ 截图确认 `MyAppBar Title` 的观感与手写版一致 |
  | 四平台构建 | ✅ Windows / Android / Gtk / Server 全 0 错误 |
  | 真机库 | ✅ 备份→还原；6 篇日记与各表计数回到原样，零残留 |
- **顺带发现一处需求缺口（不属于本次削减范围，已记进 `task_plan.md`）**：
  文档 7.3 的入口是「日课里反复出现、但没有升级成目标的念头」，而实现里 7.3 只能手动补充

## 测试结果

| 测试 | 输入 | 预期结果 | 实际结果 | 状态 |
|------|------|---------|---------|------|
| Windows 构建 | `dotnet build -f net10.0-windows10.0.19041.0 -p:WindowsPackageType=None` | 0 错误 | 0 错误 / 23 警告 / 52 秒 | ✅ |
| Windows 运行 | 直接运行 exe | 窗口出现且进程存活 | 进程 ID 28728，主窗口"侠客日记" | ✅ |
| Android 构建 | `dotnet build -f net10.0-android -c Debug` | 产出 APK | `-Signed.apk` 37 MB | ✅ |
| Android 真机运行 | — | — | **未验证**（用户暂不装，先讨论需求） | ⏳ |
| **阶段 0** Windows 构建 | `dotnet build -f net10.0-windows10.0.19041.0` | 0 错误 | 0 错误 / 20 警告 / 27 秒 | ✅ |
| **阶段 0** Android 构建 | `dotnet build -f net10.0-android -c Debug` | 0 错误 | 0 错误 / 15 警告 / 32 秒 | ✅ |
| **阶段 0** Gtk 构建 | `dotnet build SwashbucklerDiary.Gtk.slnx` | 0 错误 | 0 错误 | ✅ |
| **阶段 0** Server 构建 | `dotnet build SwashbucklerDiary.Server.slnx` | 0 错误 | 0 错误 | ✅ |
| **阶段 0** WebAssembly 构建 | `dotnet build SwashbucklerDiary.WebAssembly.slnx` | 0 错误 | **无法构建：本机缺 `wasm-tools` 工作量**（预先存在的环境缺口，非本次改动）。已核对 4 处 DI 的 `using` 与注册形态完全一致 | ⚠️ |
| **阶段 0** 真实库迁移 | 4 篇旧日记的实机库启动新构建 | 补列 + 建表 + 旧数据完好 | 20 张表 / `Kind` 可空补列 / 17 索引 / 旧数据完好 / 隐私库同步 | ✅ |
| **阶段 0** 启动与首页 | 运行 exe | 根路由渲染看板 | 标题「今日」、正文「复盘看板建设中」、左侧 4 个导航图标 | ✅ |
| **阶段 0** 4 个 tab 点击切换 | 合成鼠标点击 | 切到对应页 | **未验证**（点击未送达 WebView，窗口确为前台，原因未查明） | ⏳ |

## 错误日志

| 时间戳 | 错误 | 尝试次数 | 解决方案 |
|--------|------|---------|---------|
| 2026-10-05 | `XA5300 找不到 Android SDK 目录` + `XARAT7001 NullReferenceException` | 1 | **不是缺东西，是配置问题**：Android SDK 实际已装好，`JAVA_HOME` 也在系统变量里，但当前 bash shell 启动早于安装、未继承。显式 `export JAVA_HOME='C:\Program Files\Microsoft\jdk-17.0.20.101-hotspot'` 并加 `-p:AndroidSdkDirectory="C:\Users\GODXQI\AppData\Local\Android\Sdk"` 后成功 |

## 五问重启检查

| 问题 | 答案 |
|------|------|
| 我在哪里？ | **阶段 0~5 全部完成**。阶段 5 削减了 290 行并通过真机复验；主人已拍板收尾（那 3 个新抽象不做、改动先不提交） |
| 我要去哪里？ | 没有下一步了。要提交的话按阶段 0~5 分 commit；横切回归（隐私模式 / 整库备份恢复 / 导出导入）还没完整手测 |
| 目标是什么？ | 把侠客日记改造成《大脑训练手册》的日/周/月复盘 App，手机可日常用（已达成） |
| 我学到了什么？ | 见 `findings.md`：`MarkdownEdit` 没有设值 API、Server 预渲染不实例化页面组件、`Kind` 可空列的 null 语义、`StartPath` 指非根路由会整页空白、真机合成点击不可靠、**自搭 flex 行会宽度算飞（三个阶段记了三次）**、**i18n 扫描要按括号配对且大小写都要认、还得单独扫文案类的 const**、**灌真机数据的 Guid 末段必须 12 位**、**`InputDialog` 有 `Text` 预填参数**、**截图工具自己会骗人（DPI-unaware + 最大化 → 只截到窗口左 2/3，要拿未改动页面做对照才认得出）** |
| 我做了什么？ | 阶段 0 地基（12 表/导航/路由/迁移实测）+ 阶段 1 MVP（看板/日课/肯定库/节拍）+ 三轮 UI 追加改动 + 阶段 2 周课（2.1~2.6）/目标卡/触因/宏观目标 + 阶段 3 月课四步向导/三个优点表/归档区四 List/7.4 回流/成就增长 + 阶段 4 状态管理表/统计页/`review.json` 导出导入/周课文字化统计/设置页节拍组/i18n + 阶段 5 `/ponytail-review` 削减 290 行 + 每阶段各自的审查修复 |

## 下次恢复时的必读

1. `task_plan.md` 的「下一步」「关键问题」和「已做决策」——**尤其是「复盘不用模板系统」与阶段 2 的两处口径变更**
2. `findings.md` 的「可复用组件与机制」（**避免重造轮子**）和「阶段 1 的两条硬约束」
3. **构建命令**（阶段 0 已验证过，直接抄）：
   - Windows：`dotnet build src/SwashbucklerDiary.Maui/SwashbucklerDiary.Maui.csproj -f net10.0-windows10.0.19041.0 -c Debug -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true`
   - Android：先 `export JAVA_HOME='C:\Program Files\Microsoft\jdk-17.0.20.101-hotspot'`，再加 `-f net10.0-android -p:AndroidSdkDirectory="C:\Users\GODXQI\AppData\Local\Android\Sdk"`
     - **要交给主人安装的话必须再加 `-p:EmbedAssembliesIntoApk=true`**！不加的话是「快速部署」包，
       APK 里**一个程序集都没有**（实测 `assemblies/` = 0、38 MB），单独装必然启动即崩。
       加了之后 126 MB，程序集以 `lib/<abi>/libXXX.dll.so` 进包，才能直装。
   - **跑构建前先 `taskkill //F //IM SwashbucklerDiary.Maui.exe`**，否则 DLL 被占用会报 MSB3027
4. 主人机器上的真机库：`C:\Users\GODXQI\AppData\Local\Yu-Core\36234Yu-Core.12598F3457A93\Data\`（有备份 `Data.backup.claude`）

---

*每个阶段完成后或遇到错误时更新此文件*

### 阶段 6：随时改动 / 补足过去的复盘（2026-10-06）

- **状态：** 实现 ✅ / 四平台构建 ✅ / 真机运行期验证 ✅（数据层 4/4 全过）/ 代码审查自查 ✅ / **待主人手测界面**
- **出发点（主人原话）**：「我需要可以做到随时改动某天某周某月的日记」，真实动机是**补足过去的日记**。
  14 轮 `/grill-me` 的决议表见 `task_plan.md` 的「阶段 6」章节。

- 改动（数据层）：
  - `StumblingBlockService.SyncFromDailyAsync`：认领失败行从「**按序号**」改成「**先按描述文本、认不到再按序号**」。
    这是本次最要紧的一条——回改过去某天的日课时，中间插一条 / 删一条 / 换顺序
    **不会再让周课那边的人工判定静默地指到别人身上**（见下方 T1 实测）。
  - `WeeklyReviewModel` / `MonthlyReviewModel` += `IsSkipped`（可空，理由见下「踩到的坑 ①」）
  - `ReviewStatsService`：完成度分母扣掉「**跳过且没写**」的周期；级联方向 **月 → 周 → 天**；
    `PeriodStat` 多带一个 `Skipped` 计数，统计页缀在数字后面说明「分母为什么变小了」
  - `IWeeklyReviewService.SettleGoalAsync` **+`settledDate` 参数**：结算记在「正在做的那一周」，
    不再是 `DateTime.Now`（补 2020 年那周的周课，以前会被记成今年这一周）
  - `IMonthlyReviewService.SaveAsync` 的 `pendingGoalWeek` 改用**所选月**（3.4 的目标卡不再挂到当前周）
  - `IDailyReviewService.DeleteAsync`：删日课 = 结构化行 + 派生失败行（含已判定的）+ **那篇日记**
  - `IDailyReviewService.GetReviewDatesAsync`：日历标点用的「哪天写过」集合
  - `IVirtueService.AddAsync` **+`date` 参数**；`VirtueService` 补回 `IsAchieved` / `AchievedTime`（见下「踩到的坑 ②」）
  - 月课页：`CanEnter` 对**已定稿的月**直接放开闸门；打开已定稿的月直接落到第 4 步

- 改动（界面）：三个复盘页顶栏日期**可点开日历**（`MultiSizeDatePicker`，选「一天」再归约到周/月），
  走路由 `?Date=`（`replace: true`）并把参数读取**从 `OnInitializedAsync` 挪到 `OnParametersSetAsync`**；
  顶栏加 `MultiSizeMenu`（⋮）装「回到今天 / 去这周 / 去这月 / 删除这次日课 / 跳过这一周（这一月）」；
  日课页加「这天还没写过」提示与删除确认框；周课页加「这周日课一条都没写」引导；
  状态记录页与优点表页都能把日期指到过去；`MultiSizeDatePicker` 透传 `Events`（标点）
  与 `StateRecordEditPage.occurredTime` 的日期入口

- **真机运行期验证（实测，非推断）**——临时诊断写在独立的 `KanbanPage.Stage6Diag.cs` partial 里，
  跑完**整个文件删掉 + 删一行调用**，`grep` 确认零残留：

  | 验的是什么 | 结果 |
  |---|---|
  | **补列迁移** `IsSkipped` | ✅ 还原备份后重跑一次仍成功；`pragma table_info` 确认两表都有该列 |
  | **T1 失败行按描述匹配** | ✅ 造 `[A,B,C]` → 把中间 B 判成环境原因 → 删掉 B 重同步：<br>判过的那行**仍指向 `B-描述`**，`C-描述` 自己的行**还在**（旧逻辑下判定会指到 C、C 的行被删） |
  | **T2 删除这次日课** | ✅ 日记 / 日课行 / 失败行**三处全清**，返回 True |
  | **T3 跳过的级联扣除** | ✅ 日课 `0/7` → **`0/0`（跳过 7）**；周课 `0/1` → **`0/0`（跳过 1）**；撤销后**精确还原** |
  | **T4 结算记在所选周** | ✅ 结算记在 `2020-W10`（所选 2020-03-02 那一周），**不是**「现在这一周」`2026-W41` |
  | 真机库 | ✅ 测试前备份、测试后还原；**零 5eed 残留**；6 篇主人日记 + 日课行 + 月课行完好；`logs.db3` 零错误 |

- **踩到的坑（都已修，教训记 findings）**：
  1. **`IsSkipped` 声明成非空 `bool` → 读一次炸一次**。SqlSugar 的 `InitTables` 给已有表补列时补的是
     **可空**列（实测 DDL：`` `IsSkipped` bit ``），老行读出来是 NULL，Newtonsoft 反序列化直接抛
     `Error converting value {null} to type 'System.Boolean'`。改成 `bool?`，判定一律写 `== true`。
     —— 这正是当初 `Kind` 做成可空的原因，`InitTables` 加列时**必须可空**。
  2. **发现并修掉一个阶段 5 遗留的真 bug**：阶段 5 把 `VirtueEntryModel.IsAchieved` / `AchievedTime`
     当「可推导」删掉了，但**老库里那两列还在**（`IsAchieved bit NOT NULL`），
     而 SqlSugar **只加列、从不删列**→ 模型里没有该属性 → INSERT 不带它 → SQLite
     `NOT NULL constraint failed: VirtueEntryModel.IsAchieved`。
     也就是**往优点表插任何一条都会炸**（月课积极列表、周课 2.4 达成结算全走这条路）。
     已把两个属性补回模型并在 `AddAsync` 里照写。想彻底去掉得另加 `ALTER TABLE ... DROP COLUMN` 迁移。
  3. **手机端的日历标不了点**：`Masa.Blazor.MobileComponents.dll` 里 `PMobileDatePicker`
     **整个程序集都没有 `Events` 这个字符串**（用 python 扫的 dll，不是猜的）。
     所以 `MultiSizeDatePicker` 的 `Events` **只透给桌面端的 `DatePickerDialog`**；
     手机端靠「这天还没写过」那句页面提示 + ⋮ 里的「回到今天」兜底。

- **界面截图没能自动验证**（如实记下）：`snap-app.ps1` 用全屏 `CopyFromScreen`，
  在主人这台多窗口的桌面上会把**别的窗口**也拍进去；改用 `PrintWindow` 抓窗口本身时，
  WebView2 的内容抓不到（整张全白）。**这一项留给主人手测**，清单见 `task_plan.md`。
  注：顶栏仍是 **5 个位**（原来的「今天」换成了「⋮」），宽度与主人已经手测过的那版**一致**，
  所以本项目历史上反复栽的「自搭 flex 宽度算飞」那一类风险不适用。

- 构建：Windows / Android / Gtk / Server **全 0 错误**（删掉临时诊断后再跑一遍，同样 0 错误）
- i18n：`zh-CN` / `en-US` 各补 **16 条**（脚本 `scripts/add-i18n-stage6.py`，沿用 LF、无尾随换行、无 BOM）；
  16 条**全部在源码里命中**、两包**都无缺键**、`zh-CN` 无「值==键」的漏译

### 阶段 6 追加：日课 1.6 加「这个心情是被什么引起的」（2026-10-06）

- **主人要求**：1.6 选中某个心情芯片之后，应该有个可输入的地方记录引发心情的原因。
- **口径**：1.6 原本只有芯片（结构化进 `DailyReviewModel.Mood`，渲染在正文末尾的受管区块里），
  **没有散文格**。这次给 1.6 补一个散文章节 `MoodNote`，落在正文的 `## 1.6` 下
  ——散文进正文是既有规矩（搜索 / 导出 / 日历摘要都能读到），也不加数据库列。
- 改动：
  - `DailyReviewSections`：`NumberOfSections` 5 → 6；新增 `MoodNote`；
    `Parse`/`Compose`/`IsEmpty` 三处各补一句
  - `ReviewSectionText.dailyTitleKeys`：补第 6 个标题键 `"How was today"`（与 1.6 的芯片标题同一个键）
  - `DailyReviewPage.razor`：1.6 的标题改用 `dailyTitles[5]`；**选中情绪后**出现原因输入框
  - i18n：`zh-CN` / `en-US` 各补 1 条
- 一个刻意的取舍：**芯片取消选中之后，只要那一格还有字，输入框就留着**。
  否则「点掉芯片 → 文字凭空消失」会让人以为白写了（其实还在 `MoodNote` 里）。
- **验证**：写了个可重跑的往返核对（`scripts/roundtrip-daily-sections/`，**留在仓库里**），
  `dotnet run -c Release --project .planning/2026-10-05-self-management-system/scripts/roundtrip-daily-sections/roundtrip.csproj`
  → **`RESULT=ALL OK`，20 项全过**：六节往返、**1.4 的 `### 分块`没被新章节打乱**、受管区块不被当成正文、
  **老格式（只有 1.1~1.5）向后兼容**（1.6 空着、重拼不产生 1.6 标题、不吞区块）、
  只写原因不选情绪也读得回、手改顺序（1.6 在前）不丢字。
- **踩到的坑（自己的，记下来）**：核对脚本第一版把**带受管标记的整串**当成 `Apply(content, block)` 的
  `block` 传了进去，于是生成了双层标记、`Strip` 只剥掉内层，报出 4 条假 FAIL。
  **`Apply` 要的是区块内容本身（那几行 bullet），标记由它自己加** —— 真实调用方
  `BuildDailyFacetBlockAsync` 就是这么给的。教训：核对脚本本身也会骗人，先怀疑它。
- 构建：Windows / Android / Gtk / Server **全 0 错误**

### 阶段 6 追加：日课页顶部显示「这一周的目标」（2026-10-06）

- **主人要求**：「每周的目标应该在日课上面有所提示」。
- **依据（书里原话，不是我自己加的）**：ch12 那一节的标题就是「**每周三目标（日记的骨架）**」，
  并且写着「**日记是四步计划唯一的持续载体，每日三项积极事情 + 每周三目标是它的最小可运行结构**」。
  也就是说三目标本来就该在日课上看得见 —— 之前 1.x 里一格都没有，确实是个缺口。
- 实现：
  - `DailyReviewPage.razor.cs`：注入 `IWeeklyReviewService` + `IReviewScheduleService`，
    `LoadAsync` 里按**这一天的 ISO 周**取该周的目标（内容为空的不列）
  - `DailyReviewPage.razor`：`.review-form` 最顶部加一块只读提示，
    复用现成的 `review-picked` 样式（与「这天还没写过」同一个视觉语言，零新 CSS）；
    每条一行：`领域 · 内容（状态）`，状态用现成的 `ReviewSectionText.StatusText`
  - i18n：`zh-CN` / `en-US` 各补 1 条（`This week's goals` / 这一周的目标）
- **数据口径（关键，别弄反）**：目标卡挂在**它要完成的那一周**的周课记录上
  （月课 3.4 那条 `PushToNextWeekAsync` 就是挂到「下周」），所以按日期的 ISO 周取即可；
  那一周没开过周课 → `GetGoalsAsync` 返回空表 → 整块不显示（安静，不 nag）。
- **只读**：改目标和结算都在周课 2.3 / 2.4。日课页不给编辑，也不给点勾
  —— 结算要写原因、未达成要判「是不是环境原因」，那是 2.4 的活。
- 构建：Windows / Android / Gtk / Server **全 0 错误**；启动后错误日志 0 条
- **没验到的**：界面渲染仍然没有目视验证（截图在这台机器上不可用，见上文）。
  另外主人库里目前 `WeeklyReviewModel` / `WeeklyGoalModel` 都是 **0 行**，
  所以**开箱看不到这一块** —— 得先去周课 2.3 定一个目标才会出现。

### 阶段 6 追加：安卓 APK「装完闪退」的根因与修法（2026-10-06）

- **现象**：把 APK 装到手机上一打开就闪退。
- **根因（不用连手机就确诊了）**：`dotnet build -f net10.0-android -c Debug` **默认是「快速部署」模式**，
  产出的 APK 里**一个 .NET 程序集都没有** —— 实测解包：
  `assemblies/` 条目数 = 0，`Masa.Blazor.dll` / `SqlSugar.dll` 都是 0 个，只有资源和原生库。
  这种包假设程序集由 `dotnet build -t:Run` 通过 adb 单独推进手机；**单独安装必然启动即崩**。
- **修法**：构建时加 `-p:EmbedAssembliesIntoApk=true`。实测修好之后：
  APK 38 MB → **126 MB**，条目 1819 → 2793；程序集以 `lib/<abi>/lib<名称>.dll.so` 的形式进包
  （.NET 10 安卓就是这么装的），`SwashbucklerDiary.Shared/Rcl/Maui/Rcl.Hybird`、`Masa.Blazor`、`SqlSugar`
  **全部到位**，两套 ABI（arm64-v8a / x86_64）各 500 个，`libmonosgen-2.0.so` 与 `libmonodroid.so` 也在。
- **⚠️ 更正上一阶段的一条记录**：阶段 0 写的「产出 `-Signed.apk` 37 MB，已签名可直装」**是错的** ——
  签了名没错，但它不自包含、单独装必崩。这也解释了为什么笔记里一直写着
  「Android / Gtk / Server 只做了构建，没做运行期验证」（原来根本没验过）。
- **正确的安卓构建命令（以后照抄）**：
  `dotnet build src/SwashbucklerDiary.Maui/SwashbucklerDiary.Maui.csproj -f net10.0-android -c Debug -p:EmbedAssembliesIntoApk=true -p:AndroidSdkDirectory="..."`
- **没验到的**：本机**没有安卓模拟器**（SDK 里没有 system-images，装一套要下几个 G），
  手机也没连 USB，所以**「装上去能不能跑」仍未真机验证**。结构上确认自包含，但没运行过。
- APK 已放到 `C:\Users\GODXQI\Desktop\侠客日记-复盘版.apk`（126 MB，已签名）。

### 阶段 6 追加：删掉日课 1.5 的「1~5 精力打分」（2026-10-06）

- **主人要求**：1.5 那排 `1 2 3 4 5` 整排删掉。
- **查过才动**（两条都是动手前查实的）：
  1. 真机库 DDL 里 `"Energy" integer NULL` —— **可空**。所以删属性**不会**踩
     「NOT NULL 列删属性 → 插入炸」那个坑（阶段 1 删 `Category`/`MicroFailure` 也是同样情况，有先例）
  2. 这个分值**没有任何地方在读它**：周课没读、统计没读 —— 设计文档写的
     「连续一周偏低会被周课捡起来交给下周目标」**从来没实现过**。它只被渲染进日记的受管区块（导出时看得见），
     是个「只写不用」的数
  3. 1.5 的说明文案讲的是「记下什么给了你精力、什么让你分心」，**没提打分** → 文案不用改
- **依据（书里）**：ch05 明说**复杂变化不适合用数字衡量**（文档自己在 2.4 就引过这句），
  所以这里删掉打分、只留散文是站得住的。
- 改动（4 个文件）：
  - `DailyReviewPage.razor`：删掉那排 `MChipGroup`，留一句注释说明为什么删
  - `DailyReviewPage.razor.cs`：删掉 `SelectEnergy`
  - `DailyReviewModel`：删掉 `Energy` 属性
  - `DailyReviewService`：`HasAnyChip` 与受管区块渲染里各删一处
- **DB 列保留**（`Energy integer NULL` 还在，老数据也还在），只是模型不再映射它 ——
  **如果哪天想把这排打分加回来**，属性补回模型即可，数据没丢。
- **旧导出包不受影响**：`review.json` 用 `System.Text.Json` 默认设置读，多余字段会被忽略，
  所以带 `Energy` 的老包照常导得进来。
- **⚠️ 对设计文档的一处偏离**：文档 1.5 写的是「精力评分（1–5）」，现已按主人要求删除。
  这也是文档里那条「连续一周偏低 → 交给下周目标」的**前置条件消失**了 —— 那条本来也没实现。
- 构建：Windows / Android / Gtk / Server **全 0 错误**；
  安卓 APK 用 `-p:EmbedAssembliesIntoApk=true` 重建并**自检通过**（1000 个 .so、arm64-v8a + x86_64、
  6 个关键程序集全在），已覆盖到桌面。

### 阶段 6 追加：周课改名 + 改序（2026-10-06，主人指定）

- **主人要求**：改 5 个标题名 + 六个小节的顺序。**但字面之外藏了两个语义变化**，这才是大头：
  1. 「上周三目标结算」→「**本周**三目标结算」：2.1 结算的是**这一周自己的**目标（不再是上周的）
  2. 「每周三个目标」→「**下周**三个目标」：2.6 编辑的是**下一周**的目标
  → **新目标现在挂到「下一周」那行周课记录上**（和月课 3.4 那条「带进下周」同一个口径），
     不是挂当前这一周。链路因此变顺：**先结掉这一周 → 再回头数、提炼 → 最后定下一周**。

- **新顺序**（页面 = 章节号，两边必须一致，因为正文靠编号锚定）：
  | # | 标题 | 散文？ | 旧编号 |
  |---|---|---|---|
  | 2.1 | 本周三个目标结算 | ✅ 结算追问 | 2.4 |
  | 2.2 | 数微观失败频率 | — 结构化 | 2.1 |
  | 2.3 | 微观失败提炼 | — 结构化 | 2.2 |
  | 2.4 | 四个记录表检查 | ✅ 四问 | 2.5 |
  | 2.5 | 本周一件新事物 | ✅ | 2.6 |
  | 2.6 | 下周三个目标 | — 结构化 | 2.3 |

- 改动：
  - `WeeklyReviewSections`：锚点从「2.4/2.5/2.6」改成「**2.1/2.4/2.5**」（Parse 与 Compose 各三处）
  - `WeeklyReviewPage.razor`：六节**实际重排** + 改编号；2.1 用 `WeekRange` 且改成列 `goals`；
    2.6 列 `nextWeekGoals` 并显示 `NextWeekRange`；新建目标对话框的 `WeeklyReviewId` 传空
  - `WeeklyReviewPage.razor.cs`：`lastWeekGoals/lastWeek*` → `nextWeekGoals/nextWeek*`；
    `LoadAsync` 改成取下一周；`SaveGoalAsync` 把新目标落到**下一周**那行（`GetOrCreateAsync(nextWeekStart)`，
    仍然是「真要落目标才建行」）；`SettleAsync` 结算后刷新的是**这一周**的列表
  - `ReviewSectionText`：三个散文标题键换成新名（`Settle this week's three goals` /
    `Check the four record tables` / `One new thing this week`）
- **i18n 联动改名（最容易漏的一块）**：语言包里**有 13 条文案在正文里引用了 2.x 编号**
  （「在 2.1 里标成绊脚石」「来自 2.4：原因是环境」「只有 2.5 那一问才往这里写」……）。
  编号一换这些引用全指错，所以：
  - 键名里含编号的**连键一起改**（键就是查找 id），脚本 `scripts/reorder-weekly-i18n.py`
    同时改语言包与源码字面量（`ArchivePage` / `KanbanPage` / `WeeklyReviewPage` 三处）
  - 编号替换是**一次通过轮换**（2.1→2.2、2.2→2.3、2.3→2.6、2.4→2.1、2.5→2.4、2.6→2.5），
    **不能顺序替换**，否则 2.1 先变 2.2 又被下一轮改成 2.3
  - 每包：12 条改名 / 2 条只改值 / 删 5 个旧标题键 / 加 6 个新键；另外 11 个语言包**本来就没有**这些键，不受影响
- **扫描抓出一个我自己的真 bug**：上一轮做 1.6 时，razor 里用的是短键 `What brought this mood on`，
  而语言包里加的是长键 —— **界面上会露出英文原文**。已统一成长键。
  （教训：改完 i18n 一定要跑 `scan-i18n-all.py`，不能只靠构建。）
- **验证**：往返检查（`scripts/roundtrip-daily-sections/`）**扩到周课**，`RESULT=ALL OK`：
  2.1/2.4/2.5 写散文、2.2/2.3/2.6 不写、三节往返一致、多行续接正确；
  另打印一条 **NOTE**：老格式周课正文会按新编号重读（主人的库里 **0 条周课**，无实际影响）。
- 构建：Windows / Android / Gtk / Server **全 0 错误**；APK 已用 `-p:EmbedAssembliesIntoApk=true`
  重建、自检通过（1000 个 .so、arm64-v8a + x86_64、无缺失）、覆盖到桌面。
- **对设计文档的偏离**：文档第 2 节的六个小节名与顺序现在都变了；文档 7.2 那句
  「周课 2.3 定下的目标」在实现里已改成 2.6。文档原文未改（它是主人的原始方法文档）。

### 阶段 6 追加：2.1 结算重设计（2026-10-06，`/grill-me` 定案后实施）

- **主人要求**：① 2.1 里那句重复的话删掉；② 不该只有一个文本框 —— 要列出本周目标 + 「完成/未完成」，
  选否则展开当前目标的**注释**：目标内容 + 当时写下的 S/M/A/R/T；S/M/A/R/T 是选择框+文本描述，
  选中的要在文本框里写原因。
- **动手前查实的三件事**：
  1. 重复那句**确认存在**：小标题问「这一周，有没有什么不一样的事…」，下面说明的**第一句又是同一句**
  2. 目标对话框**确实收集并保存了** S/M/A/R/T 五项（`SmartSpecific`~`SmartTimeBound`）→ 归因有料可展示
  3. 旧结算弹窗 `GoalSettleDialog` **只有周课页在用** → 可放心整体删掉
- **grill 定案**（含我原本想错的两处，主人当场纠正）：
  | 问题 | 定案 |
  |---|---|
  | 完成状态 | **二选一：完成 / 未完成**（`GoalStatus.InProgress` 枚举值**保留**但界面不再给 —— 删了会让 Done/Dropped 重新编号，老数据静默错位） |
  | 「原因是环境」怎么办 | **不单独设**，由 SMART 的 A 承担；**7.4 那条路随之断掉**（结算不再写归档） |
  | 归因文本存哪 | `WeeklyGoalModel` **+5 个可空列** `SmartSpecificNote`…（非空 = 勾了，不另存勾选标记） |
  | 达成之后 | 仍旧自动写进 6.3 成就列表（不需要填东西，符合「选是则无事」） |
  | 结算说明文本框 | 取消（原因已按 SMART 分项写了） |
  | 勾了没写原因 | **主动保存时拦下来**；自动保存不拦（弹提示没人看得见，且拦下来会连当天散文一起存不上） |
  | 落库时机 | 跟页面现有保存走：点选立即写 Status；勾选与原因走「脏了就存」 |
- 改动：`WeeklyGoalModel` +5 列；`SettleGoalAsync` 简化成 `(goal, settledDate, status)` 并删掉 7.4 那段写入；
  `WeeklyReviewService` 里 `IArchiveService` 成了死依赖 → 连字段/构造参数一起删掉；
  `GoalSettleDialog`（.razor + .cs）与 `GoalSettlement` 记录**整个删除**；
  页面 2.1 换成「目标卡 + 二选一 + 未达成归因区」；**SMART 五问收进 `ReviewSectionText`**，
  目标对话框与 2.1 共用一份（原先两处各存一份，改一处漏一处就会出现同一个字母两种说法）。
- **选择框用的是芯片不是 `MCheckbox`**：项目里 `MChip` 用了 46 处、`MCheckbox` **一次都没用过**，
  而本项目在「自搭控件」上栽过三次 —— 所以跟随既有视觉语言。
- **真机运行期验证（临时诊断，验完已删、`grep` 确认零残留）**：
  | 验的是什么 | 结果 |
  |---|---|
  | **五个新列的迁移** | ✅ DDL 实测补成**可空** `varchar(255)`（没踩 NOT NULL 那个坑）；还原备份后重开**又补一次**同样成功 |
  | 五个新列可读写 | ✅ 写了的三条读得回、没写的两条是 null |
  | 未达成**不再**写 7.4 | ✅ 归档条数 `1 -> 1` 不变 |
  | 达成仍写成就列表 | ✅ 成就 `0 -> 1` |
  | 结算仍记在**所选那周** | ✅ `2020-W10` |
  | 真机库 | ✅ 测前备份、测后还原；**零测试残留**；主人数据（3 篇日记 / 3 条失败行 / 1 条归档）完好 |
- **诊断脚本自己出的错（教训）**：第一版给目标**预填了 Id**，而 `SaveGoalAsync` 只在 `Id == Guid.Empty` 时才插入、
  非空走 UPDATE → **更新了 0 行、静默什么都不发生**，四条断言全 FAIL 差点误判成产品 bug。
  修法：Id 留空 + **把中间步骤的返回值都打出来**（`save=True settle=True`）——
  「诊断只断言最终结果、不打印中间返回值」是这次差点绕远路的根因。
- **踩到的环境坑**：Gtk 构建报 `CS2012 无法写入 SwashbucklerDiary.Rcl.dll ... 被 VBCSCompiler 锁定` ——
  不是代码错（紧接着 Server 就过了），`dotnet build-server shutdown` 后重跑即过。
- 构建：Windows / Android / Gtk / Server **全 0 错误**；APK 自包含重建、自检通过（1000 个 .so、两套 ABI、无缺失）、已覆盖桌面。
