# 实施计划：日记跨端同步（WebDAV / 坚果云）

**任务目录**：`D:\Src\Git\GH_SwashbucklerDiary\.planning\2026-11-15-diary-sync`
**目标平台**：Android + Windows（.NET MAUI）
**存储载体**：坚果云 WebDAV（`https://dav.jianguoyun.com/dav/`）
**设计来源**：Anki 同步机制的精简版（墓碑 + 对象级 LWW + 全量兜底），去掉服务端会话与分块协议
**预估工作量**：单人 7 天（阶段 0–7）

---

## 1. 目标与非目标

### 目标

1. Android 与 Windows 之间同步日记、标签、附件索引、15 张复盘表、地点。
2. 删除能传播，被删除的日记不会从另一端"复活"。
3. 两端修改同一对象时，较新版本胜出，较旧版本保留为「冲突副本」而非静默丢弃。
4. 同步中断不产生半坏状态；重试后结果与一次成功一致。
5. 两端同时同步时能检测冲突并提示重试。
6. 不破坏现有整库备份/恢复功能。

### 非目标（第一版明确不做）

| 不做 | 原因 |
|---|---|
| 同步设置项（主题、锁屏、WebDAV 配置） | 两端配置互相打架，比不同步更糟 |
| 字段级合并 | 需要 CRDT，收益远小于成本；只做对象级 LWW |
| 实时同步 / 推送 | WebDAV 无推送通道；只做手动 + 启动时触发 |
| 同步隐私模式库 | `PrivacyDatabaseFilename` 为独立库，首版排除，UI 明示 |
| 多用户 / 共享 | 单账号单同步域 |
| 同步 Log / 成就 / 用户状态 | 可由日记重建；同步反而引入冲突 |

---

## 2. 坚果云准备（阶段 0 前由用户完成）

### 2.1 配置步骤

1. 登录坚果云网页版 → 右上角头像 → **账户设置** → **安全选项** → **第三方应用管理**。
2. 添加应用（名称随意，如 `SwashbucklerDiary`），生成**应用密码**。
3. 在 App 设置 → 备份 → WebDAV 中填入：

| 字段 | 值 |
|---|---|
| 服务器地址 | `https://dav.jianguoyun.com/dav/` |
| 账号 | 坚果云注册邮箱 |
| 密码 | **应用密码**（不是登录密码） |

现有代码 `WebDAV.Set()` 会在目录不存在时自动 `MKCOL` 创建 `SwashbucklerDiary/`，无需手动建目录。

### 2.2 已知限制（写进 UI 提示与文档）

| 项 | 值 | 影响 |
|---|---|---|
| 免费版流量 | 每月上传约 1 GB、下载约 3 GB（以官网为准） | 纯文本日记一年几 MB，够用；附件同步需按需 |
| 单文件大小 | 免费版有上限（约 500 MB） | 大视频跳过并提示 |
| 请求频率 | 有并发/频率限制 | 附件批量上传需串行 + 限速 |
| 目录深度 | 支持多级，但需逐级 `MKCOL` | 初始化时逐级创建 |
| PROPFIND 深度 | 不支持 `Depth: infinity` | 设计上只用 `Depth: 1` 或直接 GET manifest |

---

## 3. 远端目录结构

```
SwashbucklerDiary/
├── SwashbucklerDiaryBackup_*.zip     ← 现有整库备份，保留不动
└── Sync/
    ├── manifest.json                 ← 唯一提交点
    ├── diaries/
    │   └── {guidN}.json
    ├── review/
    │   ├── daily.json
    │   ├── weekly.json
    │   ├── weeklyGoal.json
    │   ├── stumblingBlock.json
    │   ├── monthly.json
    │   ├── limitingBelief.json
    │   ├── enemy.json
    │   ├── virtueEntry.json
    │   ├── affirmation.json
    │   ├── archiveItem.json
    │   ├── macroGoal.json
    │   ├── stateRecord.json
    │   ├── lesson.json
    │   ├── lessonTypeLink.json
    │   └── dailyEvent.json
    ├── locations.json
    └── assets/
        ├── Image/{sha256}.{ext}
        ├── Audio/{sha256}.{ext}
        └── Video/{sha256}.{ext}
```

### 3.1 manifest.json（schema v1）

```json
{
  "schema": 1,
  "appVersion": "0.0.7",
  "updatedAt": "2026-11-15T14:32:11Z",
  "deviceId": "c0ffee00-...",
  "diaries": {
    "<guidN>": { "u": 1763200000123, "h": "<sha256hex>" }
  },
  "review": {
    "daily": { "h": "<sha256hex>" }
  },
  "locations": { "h": "<sha256hex>" },
  "assets": {
    "Image/<sha256>.jpg": { "h": "<sha256hex>" }
  },
  "graves": {
    "diary:<guidN>": 1763200000123,
    "tag:<guidN>": 1763200000123,
    "asset:Image/<sha256>.jpg": 1763200000123
  }
}
```

约定：

- `u` = 对象自身 `UpdateTime` 的 Unix 毫秒。**不依赖 WebDAV 服务器的时钟**。
- `h` = 文件内容的 SHA256，用于判断是否需要传输。
- `graves` 保留 90 天；过期条目在提交时清理（长期离线设备可能"复活"数据，在文档中说明）。
- manifest 是**唯一提交点**：先传内容文件，最后 CAS 提交 manifest。中断只留孤儿文件，可被后续垃圾回收。

---

## 4. 本地改动

### 4.1 新增墓碑表

```csharp
// src/SwashbucklerDiary.Shared/Models/SyncGraveModel.cs
public class SyncGraveModel : BaseModel
{
    public string EntityType { get; set; } = "";  // diary / tag / review:{table} / asset
    public Guid EntityId { get; set; }
    public DateTime DeletedTime { get; set; }
}
```

注册到 `src/SwashbucklerDiary.Rcl/Extensions/ServiceCollectionExtensions/AddSqlsugar.cs` 的 `InitTables` 列表（约 369 行）。CodeFirst 自动建表，不改旧表结构。

### 4.2 新的 Preferences 键

| 键 | 类型 | 含义 |
|---|---|---|
| `SyncDeviceId` | string | 首次同步时生成，用于冲突副本标注 |
| `SyncLastSessionStart` | string(ISO) | 上次成功同步**开始**时的本地时间 |
| `SyncLastManifestHash` | string | 上次成功同步时的 manifest 哈希 |
| `SyncAutoOnStart` | bool | 启动时自动同步开关 |
| `SyncIncludeAssets` | bool | 是否同步附件（默认 false） |

### 4.3 UpdateTime 兜底

现状：`UpdateTime` 由 30 多处业务代码手写赋值，漏一处就漏同步。
做法：在 `AddSqlsugar.cs` 的 AOP `DataExecuting` 中拦截 `Update`/`Insert`，若 `UpdateTime` 与库中值相同或未变更则强制刷新为 `DateTime.Now`。

> 注意：AOP 只能拿到实体，拿不到旧值。保守做法是**只在 Update 时强制刷新 `UpdateTime`**，Insert 依赖模型默认值。

### 4.4 删除拦截

`BaseDataService.DeleteAsync` 的 5 个重载统一加墓碑：

```csharp
// 删除前
if (entity is BaseModel bm && SyncEntityTypes.Contains(typeof(TEntity)))
    await _graveService.AddAsync(EntityKey(typeof(TEntity)), bm.Id);
```

- `DeleteAsync(Guid id)` 直接可用。
- `DeleteAsync(Expression<...>)` 需要**先查再删**。
- `DiaryRepository` 里直接操作 `Context.Deleteable` 的路径（如 `MovePrivacyDiariesAsync`）需要逐个补墓碑，搜索 `Deleteable<` 确认清单。

---

## 5. 同步算法

```
SyncAsync(ct):
  sessionStart = Now
  EnsureRemoteDirs()
  (manifest, etag) = GetManifest()            // 不存在则视为空

  # 首次 / 空库
  if 本地空 && manifest 空:
      return NoChanges
  if 本地空 && manifest 非空:
      下载全部对象 → 导入 → 提交空 diff
      return
  if 本地非空 && manifest 空:
      全量上传 → 提交 manifest
      return

  if manifest.schema != 1:
      提示「云端数据结构版本不一致」
      提供「以本机为准上传」/「以云端为准下载」两个按钮（复用现有 DB 备份）
      return

  # 1. 墓碑
  for (key, deletedAt) in manifest.graves:
      本地实体存在 ?
        实体.UpdateTime <= deletedAt → 删除本地（不再写新墓碑）
        实体.UpdateTime >  deletedAt → 保留，本次提交时移除该墓碑
  localGraves = SyncGrave 表 where DeletedTime > lastSessionStart
  for g in localGraves:
      manifest.graves[key] = g.DeletedTime
      删除远端对应对象文件

  # 2. 拉云端
  for (id, meta) in manifest.diaries where meta.u > lastSessionStart:
      remote = GET diaries/{id}.json
      local  = 本地同 Id
      if local 存在 && local.UpdateTime > lastSessionStart && hash(local) != hash(remote):
          CreateConflictCopy(local)           # 先备份本地版本
          Apply(remote)
      else if local 不存在 || local.UpdateTime < remote.UpdateTime:
          Apply(remote)

  # 3. 推本地
  for d in 本地日记 where d.UpdateTime > lastSessionStart || !manifest.diaries.ContainsKey(d.Id):
      PUT diaries/{id}.json
      manifest.diaries[id] = { u: d.UpdateTime, h: sha256 }

  # 4. 复盘表（15 张）
  for (table, meta) in manifest.review:
      remote = GET review/{table}.json
      merged = RowLevelLWW(本地表, remote.rows)   # 按行 Id + UpdateTime
      写回本地表
      if merged != remote: PUT review/{table}.json
      manifest.review[table] = { h: sha256(merged) }

  # 5. 地点
  同复盘单表处理

  # 6. 附件（SyncIncludeAssets == true）
  localAssets  = 所有 ResourceModel + 头像，按 SHA256 文件名
  remoteAssets = manifest.assets
  for a in localAssets - remoteAssets: PUT assets/{path}
  for a in remoteAssets - localAssets:
      if 本地有墓碑 → manifest.graves[asset:...] = t
      else GET assets/{path} 并写入本地目录
  for g in 本地附件墓碑: 删除远端文件

  # 7. 提交
  manifest.updatedAt = Now
  PUT manifest.json with If-Match: etag
    → 412: 另一台设备刚提交 → 重新拉 manifest，仅重试一轮
    → 成功: 记录 LastSessionStart = sessionStart, LastManifestHash
```

### 5.1 冲突副本规则

```
CreateConflictCopy(local):
    新 Id = Guid.NewGuid()
    Title  = $"{local.Title}（冲突副本 {Now:yyyy-MM-dd HH:mm}）"
    Content = $"> ⚠ 同步冲突时保留的版本，来自 {deviceName}。\n" + local.Content
    UpdateTime = Now
    Add(副本)
```

### 5.2 复盘行级 LWW

```csharp
merged = 本地行 ∪ 远端行，按 Id 分组：
    只有一边       → 取该边
    两边都有       → UpdateTime 大的胜出
    一边有墓碑     → 墓碑时间 > UpdateTime 则删除，否则保留并在本次提交移除墓碑
```

---

## 6. 阶段划分与验收标准

### 阶段 0：坚果云连通性探测（0.5 天）—— ✅ 已完成（2026-11-15）

- [x] `PROPFIND /dav/SwashbucklerDiary/` 正常返回 → 207
- [x] 逐级 `MKCOL`：`Sync/`、`Sync/diaries/`、`Sync/review/`、`Sync/assets/Image/` 成功
- [x] `PUT` 测试文件 → 204，**响应无 ETag**
- [x] `GET` 带 `If-None-Match` → 304
- [x] `PUT` 带错误 `If-Match` → **412**
- [x] 连续 PUT 50 个小文件 → 50/50 成功，均值 **163.6 ms/次**
- [x] `DELETE` 目录/文件正常 → 204，删除后 GET 404

**结论**：`If-Match` + ETag 完整可用，**并发控制走 CAS，不实现 Plan B**。详见同目录 `findings.md`。

实现约束（由探测得出）：

1. ETag 形如 `TisiKPx-feuWwHcna9T80A`，**不带引号**，必须原样回传，不要按 RFC 加引号。
2. `PUT` 不返回 ETag，需要时再 `GET` 一次。
3. `GET` 不返回 `Last-Modified`，禁止依赖服务器时间戳。
4. 单文件 PUT 约 164 ms → 首次同步改用 zip 一次性上传，见阶段 3。
5. 50 次连续 PUT 未触发限流 → 附件同步可用 4–6 并发。

### 阶段 1：同步基础设施（1 天）—— ✅ 已完成（2026-11-15）

- [x] `SyncGraveModel` + `AddSqlsugar` 注册
- [x] `ISyncStorage` 抽象：`GetAsync` / `PutAsync(ifMatch)` / `DeleteAsync` / `EnsureDirectoryAsync` / `ListAsync` / `ExistsAsync`
- [x] `WebDavSyncStorage` 实现（扩展 `IWebDAV`，补 `GetFileAsync`、`PutFileAsync`、`DeleteFileAsync`、`MakeCollectionAsync`、`GetETagAsync`、`ExistsAsync`、`ListAsync`）
- [x] `LocalSyncStorage`（本地目录实现，用于离线模拟双端）
- [x] SyncState Preferences 读写
- [x] UpdateTime 兜底 AOP + `SyncContext` 抑制开关
- [x] `SyncService.ProbeAsync` 自检 + DEBUG 入口

**验收**：Rcl / Rcl.Hybird / Gtk 编译通过；真机自检 8 项待跑。

**实现记录**：

1. **SqlSugar AOT 包裁掉了 `DataOperationType` 枚举**，AOP 改用 `OperationType.ToString()` 判断 `"Add"` / `"Update"`。这个字符串对不上会静默失效，自检第 1 项专门验它。
2. **墓碑固定落主库**：`SyncGraveRepository` 覆盖 `Context`，不跟随隐私模式切库。
3. **`SyncContext` 用 `AsyncLocal`**：同步导入时必须保留远端对象的 `UpdateTime`，否则 LWW 失效。
4. **`Updateable(entity).UpdateColumns(columns)` 的坑**：columns 不含 `UpdateTime` 时 AOP 无效，阶段 3 前需统一排查调用点。

### 阶段 2：manifest 与 diff 引擎（1 天）—— ✅ 已完成（2026-11-15）

- [x] `SyncManifest` / `SyncObjectMeta` 模型（短属性名、Unix ms、SHA256）
- [x] `SyncLocalSnapshot`、`SyncPlan`、`SyncChange`、`SyncEntityType` / `SyncAction`
- [x] `SyncEntityKeys`：统一键拼接与解析，两端字符串必须逐字节一致
- [x] `SyncTime`：`DateTime` ↔ Unix ms，集中处理 `DateTimeKind`
- [x] `SyncDiff` 纯函数：远端墓碑 → 本地墓碑 → 对象 diff
- [x] 单测 14 个：空库、LWW、窗口内冲突、首次同步、墓碑两侧、复盘/地点/附件
- [x] 不触碰网络与数据库

**验收**：`dotnet test tests/SwashbucklerDiary.Sync.Tests` → 14/14 通过。

**实现记录**：

1. **冲突判定不用单纯时间戳**：两端都在 `lastSessionStart` 之后改过、且内容哈希不同 → `Conflict`，
   执行层先备份本地为副本再应用远端。只比时间戳会在"A 改标题、B 改正文但时间戳更旧"时静默丢编辑。
2. **墓碑优先于对象 diff**：已由墓碑处理的 key 进入 `handled` 集合，避免同一对象既删除又上传。
3. **本地墓碑只推窗口内的**：`deletedAt <= lastSessionStart` 视为上一轮已推送，跳过，防止 manifest 膨胀。
4. **首次同步**：`lastSessionStart == default` → `long.MinValue`，所有对象都算"窗口内修改"，
   同 Id 不同 hash 直接判冲突并保留副本。

### 阶段 3：日记双向同步（1.5 天）—— ✅ 代码完成（2026-11-15），待真机验收

- [x] `ISyncObjectCodec` / `SyncObjectCodec`：规范化序列化 + SHA256，保证跨设备哈希稳定
- [x] `ISyncSnapshotService` / `SyncSnapshotService`：日记（含模板）、附件、墓碑
- [x] `ISyncManifestStore` / `SyncManifestStore`：manifest 读写 + ETag
- [x] `ISyncExecutor` / `SyncExecutor`：上传、下载、冲突副本、删除
- [x] `SyncService.SyncAsync`：状态机、CAS 提交、412 重试一轮
- [x] 删除拦截 + 墓碑写入（`DiaryService` 五个重载）
- [x] 同步前自动本地 DB 备份，保留最近 3 份
- [x] 临时入口「立即同步（DEBUG）」

**验收**：待真机跑首轮同步（见第 8 节 1–5）。

**实现记录**：

1. **放弃 zip 初始化**：zip 上传后远端没有单文件对象，后续增量仍会逐篇 PUT（除非服务端能解压）。
   收益不成立，改为逐篇上传；首次 1000 篇约 2.7 分钟，后续增量只有几篇。
2. **删除顺序**：先删除、成功后再写墓碑。反过来在删除失败时会把删除传播到远端，造成两端一起丢数据。
3. **冲突副本**：应用远端前先把本地版本存为「冲突副本」日记，标题带时间、正文注明来源设备。
4. **应用远端时双重抑制**：`SyncContext` 同时抑制 `UpdateTime` 改写与墓碑记录。

### 阶段 4：复盘与地点（1 天）—— ✅ 代码完成（2026-11-15），待真机验收

- [x] `SyncReviewTables`：15 张表类型 ↔ 短键映射
- [x] `SyncReviewMerger`：逐表行级 LWW + 行墓碑 + 稳定排序
- [x] `SyncObjectCodec`：复盘/地点序列化（每表按 Id 排序保证哈希稳定）
- [x] `SyncSnapshotService`：采集复盘整包与地点
- [x] `SyncExecutor`：复盘/地点合并执行 + 回传合并结果
- [x] 行删除墓碑：`ReviewRepository<T>` 统一拦截 15 张表，`LocationService` 拦截地点
- [x] `IReviewExportService.ApplyMergedAsync`：删除差集 + upsert，尊重 `SyncContext`
- [x] 6 个合并测试（不同行/同行/墓碑/复活/排序）

**验收**：25/25 测试通过；真机待验。

**实现记录**：

1. **复盘用单个 `review.json`，不是每表一个文件**：复盘数据量小（几百 KB），行级合并在文件内部完成，
   不会因为"整包哈希变了"丢数据；少 14 个远端文件和 14 次请求。
2. **墓碑键带表名**：`review:{table}:{rowId}`、`location:{rowId}`。`SyncEntityKeys` 已支持 `location:` 前缀，
   `SyncDiff.ResolveLocal` 对行级墓碑返回 null，交给执行器在合并阶段处理。
3. **合并后回传**：如果本地有远端没有的行（或本地较新），合并结果与远端字节不同就 PUT 回去，
   一次同步内两端收敛。
4. **应用远端时保留 UpdateTime**：`ReviewExportService.MergeAsync` 的显式赋值也受 `SyncContext` 控制。

### 阶段 5：附件同步（1 天）—— ✅ 代码完成（2026-11-15），待真机验收

- [x] `SyncAssetPath`：键 `{MediaResource}/{相对路径}` ↔ 本地/远端路径，拒绝 `..`
- [x] 快照采集附件（文件名即 SHA256 时不再读文件）
- [x] 执行器：上传/下载/删除本地/删除远端，4 路并发
- [x] 超过 500 MB 跳过并记录错误，不阻断整体
- [x] 附件删除墓碑：`ResourceService.DeleteResourceFiles` 在清理文件前写 `asset:{键}`
- [x] 7 个路径测试，总计 32 个测试全绿
- [ ] 头像同步：**不做**。`Avatar` 是设置项，而设置整体不同步；
      只同步文件会让另一端不知道用哪张图，反而制造不一致。

**验收**：真机待验。

**实现记录**：

1. **并发 4 路，无指数退避**：单个附件失败记录到 `Errors`，下一轮同步自然重试；
   对个人日记的量级，退避重试的复杂度不值得。
2. **删除墓碑在文件清理前写**：`DeleteResourceFiles` 先枚举出"将被删除"的文件，
   逐个写 `asset:` 墓碑，再调 `ClearFolderAsync`。
3. **远端键同样是外部输入**：`SyncAssetPath.TryParse` 拒绝含 `..` 的相对路径。

### 阶段 6：UI 与触发（1 天）—— ✅ 代码完成（2026-11-15），待真机验收

- [x] 备份页新增「Sync」tab（Web 版无 WebDAV 实现，自动隐藏）
- [x] `SyncPage`：上次同步时间、立即同步、结果摘要、错误提示
- [x] 附件同步开关（默认关）、启动时自动同步开关（默认关）
- [x] 隐私模式禁用同步并提示
- [x] 自动同步：`MainLayoutBase` 启动后后台执行，失败只写日志
- [x] 行墓碑与 manifest graves 保留 90 天，过期清理
- [x] 删除 WebDAV 备份页的 DEBUG 入口
- [x] `zh-CN.json` / `en-US.json` 文案

**未做**：首次同步的「以本机为准 / 以云端为准」二选一。
默认合并已经覆盖正常场景，强行覆盖是最容易丢数据的路径，不值得为极少数情况提供。

**验收**：真机点同步有结果摘要；开关持久化；隐私模式提示正确。

### 阶段 7：真机验收与文档（0.5 天）

- [ ] Windows ↔ Android 双向实测（坚果云）
- [ ] 核对流量消耗、同步耗时
- [ ] 写用户说明（坚果云配置、冲突副本含义、隐私库限制）
- [ ] 回归：整库备份/恢复、局域网传输不受影响

---

## 7. 代码改动清单

| 文件 | 改动 | 规模 |
|---|---|---|
| `src/SwashbucklerDiary.Shared/Models/SyncGraveModel.cs` | 新增 | ~10 行 |
| `src/SwashbucklerDiary.Rcl/Extensions/ServiceCollectionExtensions/AddSqlsugar.cs` | 注册表、AOP 刷新 UpdateTime | ~20 行 |
| `src/SwashbucklerDiary.Rcl/Services/Sync/ISyncService.cs` | 同步入口与结果 | ~40 行 |
| `src/SwashbucklerDiary.Rcl/Services/Sync/SyncService.cs` | 主流程编排 | ~350 行 |
| `src/SwashbucklerDiary.Rcl/Services/Sync/SyncManifest.cs` | manifest 模型 | ~80 行 |
| `src/SwashbucklerDiary.Rcl/Services/Sync/SyncDiff.cs` | 纯函数 diff 引擎 | ~200 行 |
| `src/SwashbucklerDiary.Rcl/Services/Sync/SyncMerge.cs` | 日记/复盘/墓碑合并 | ~250 行 |
| `src/SwashbucklerDiary.Rcl/Services/Sync/ISyncStorage.cs` | 存储抽象 | ~30 行 |
| `src/SwashbucklerDiary.Rcl.Hybird/Services/WebDAV/WebDavSyncStorage.cs` | WebDAV 实现 | ~150 行 |
| `src/SwashbucklerDiary.Rcl.Hybird/Services/WebDAV/IWebDAV.cs`、`WebDAV.cs` | 补文件操作方法 | ~80 行 |
| `src/SwashbucklerDiary.Rcl/Services/Data/BaseDataService.cs` | 删除拦截 | ~30 行 |
| `src/SwashbucklerDiary.Rcl.Hybird/Pages/Mine/Backups/SyncPage.razor(.cs)` | UI | ~200 行 |
| `src/SwashbucklerDiary.Rcl/wwwroot/i18n/*.json` | 文案 | ~20 键 |

**合计约 1500 行**，其中逻辑集中在 `SyncDiff` 与 `SyncMerge`，其余为胶水。

---

## 8. 验收场景清单

| # | 场景 | 期望 |
|---|---|---|
| 1 | 云端空，本地有数据 | 全量上传，manifest 正确 |
| 2 | 本地空，云端有数据 | 全量下载，日记/复盘/附件齐全 |
| 3 | A 写一篇，B 同步 | B 出现该篇，A 无变化 |
| 4 | A 删除一篇，B 同步 | B 删除该篇，不复活 |
| 5 | A、B 改同一篇 | 较新者生效，较旧者成为冲突副本 |
| 6 | 同步中断（断网/杀进程） | 重试后结果一致，无半坏数据 |
| 7 | A、B 同时同步 | 一方 CAS 失败（或锁冲突），提示重试，重试后一致 |
| 8 | 附件增删 | 两端一致，无重复文件 |
| 9 | 隐私模式开启 | 同步按钮禁用，提示原因 |
| 10 | 老版本备份 zip | 仍可正常导入，不受影响 |
| 11 | 复盘两端改不同行 | 两行都保留 |
| 12 | 复盘两端改同一行 | UpdateTime 新者胜出 |

---

## 9. 风险与回退

| 风险 | 概率 | 应对 |
|---|---|---|
| ~~坚果云不支持 `If-Match`~~ | ~~中~~ | **已排除**：阶段 0 实测返回 412，CAS 可用 |
| 免费流量不足（附件多） | 高 | 默认只同步文字；附件手动触发；UI 显示本月已用量（可选） |
| 单文件超限（视频） | 中 | 跳过并汇总提示，不阻断整体同步 |
| manifest 过大/过慢 | 低 | 几千篇约数百 KB；超过 2 MB 时拆分为 `manifest/{deviceId}.json` 分片 |
| UpdateTime 漏更新 | 中 | AOP 兜底；上线前扫描所有 `UpdateAsync` 调用点 |
| 同步中途数据结构变更 | 低 | `schema` 版本不匹配时强制走全量二选一 |
| 数据损坏 | 低 | 每次同步前自动本地备份，保留 3 份 |
| 隐私库被误同步 | 低 | 首版硬性排除；`GetCurrentDatabasePath` 已按隐私模式切换，同步逻辑固定读主库 |

---

## 10. 实施顺序建议

```
阶段 0（探测） → 阶段 1（基础设施） → 阶段 2（diff 单测）
     → 阶段 3（日记） → 阶段 6 最小 UI（先能点同步）
     → 阶段 4（复盘/地点） → 阶段 5（附件） → 阶段 6 补全 → 阶段 7 验收
```

阶段 3 完成后就先接一个最小 UI，让真机可以点同步——这样后续三个阶段都能在真机上边做边验，而不是全部做完才知道有问题。
