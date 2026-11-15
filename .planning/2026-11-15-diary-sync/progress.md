# 进度

## 当前阶段

阶段 1 已完成并**真机验收通过**（2026-11-15 11:41，Android 自检 8 项全 PASS）。
随后按代码审查意见完成 P1 + P2 修复，重新构建并复验通过。

## 下一步

阶段 3：日记双向同步（本地快照采集、JSON 读写、冲突副本、删除拦截、同步前备份）。

## 已完成

### 阶段 0：坚果云 WebDAV 能力探测
- [x] 十项探测全过（见 `findings.md`）
- 探测脚本：`D:\Temp\jgy-probe\probe.ps1`

### 代码审查修复（阶段 6 后）

- P2：`SyncPage.OnResume` 先读状态再触发渲染，上次同步时间不再显示旧值
- P2：`WebDavConfigForm` 改用源生成上下文 `WebDavJsonContext`，三处反序列化统一
- P2：同步失败时弹窗附带前 3 条错误详情
- P2：自动同步距上次成功不足 5 分钟时跳过，避免冷启动全量快照
- P2：`tabListItems` 在 `OnInitialized` 构建一次
- P3：墓碑清理失败只记警告，不影响同步结果
- P3：`SyncPage` 新增「诊断」入口（`ProbeAsync`）
- P3：`zh-TW` 补齐阶段 6 文案

### 阶段 6：UI 与触发
- [x] 备份页「Sync」tab + `SyncPage`
- [x] 附件开关（默认关）、启动自动同步开关（默认关）
- [x] 隐私模式禁用
- [x] 自动同步接入 `MainLayoutBase`
- [x] 墓碑 90 天清理（本地表 + manifest）
- [x] 删除 DEBUG 入口
- [x] 中英文文案
- [ ] 真机验收：同步页交互、开关持久化、自动同步

### 代码审查修复（阶段 5 后）

- P1：`SyncAssetPath.TryParse` 改为逐段校验，拒绝空段、`.`/`..`、反斜杠、冒号
- P1：附件并发块写 `remote.Assets` 加锁（Dictionary 非线程安全）
- P2：附件下载加 500 MB 上限，超限删除半截文件
- P2：隐私模式下清理附件不写墓碑（附件目录两库共享，避免误删主库引用）
- P2：`SyncExecutor` 拆成 `SyncDiaryHandler` / `SyncAssetHandler` / `SyncReviewHandler`，主类只做分发
- P3：新增 5 个不安全路径用例，测试总数 37
- P3：`ClearFolderAsync` 内部已对排除路径做 `Path.GetFullPath`，无需额外统一

### 阶段 5：附件同步
- [x] `SyncAssetPath` 键/路径转换与穿越防护
- [x] 执行器附件上传/下载/删除，4 路并发
- [x] 500 MB 上限跳过
- [x] `ResourceService` 删除文件时写墓碑
- [x] 32 个测试全绿
- [ ] 真机验收：增删图片/音频、大视频跳过
- 头像不同步（与"设置整体不同步"保持一致）

### 代码审查修复（阶段 4 后）

- P1：地点同步从「清空再重插」改为「删除差集 + 逐行 upsert」，避免中途失败清空整表
- P2：两端都无复盘数据时不再生成空 `review.json`
- P2：`ReviewRepository.DeleteAsync()` 改为按查出的列表删除，消除漏墓碑窗口
- P2：复盘/地点每轮只合并一次（去重）
- 待办（阶段 6）：行墓碑 90 天清理，防止 manifest 持续增长

### 阶段 4：复盘与地点
- [x] 15 张表行级 LWW 合并（`SyncReviewMerger`）
- [x] 复盘/地点快照与序列化
- [x] 执行器合并 + 回传
- [x] 行删除墓碑（`ReviewRepository<T>`、`LocationService`）
- [x] 6 个合并测试，总计 25 个测试全绿
- [ ] 真机验收：两端改不同行、删除行传播、地点增删

### 阶段 3：日记双向同步
- [x] `SyncObjectCodec`：规范化序列化 + 跨设备稳定哈希
- [x] `SyncSnapshotService`：日记/附件/墓碑采集，附件文件名即哈希时不再读文件
- [x] `SyncManifestStore`：manifest 读写 + ETag
- [x] `SyncExecutor`：上传/下载/冲突副本/删除
- [x] `SyncService.SyncAsync`：CAS 提交、412 重试、同步前备份
- [x] `DiaryService` 删除拦截
- [x] 临时入口「立即同步（DEBUG）」
- [ ] 真机验收：首轮上传、双端拉取、删除传播、冲突副本、中断重试

### 阶段 2：manifest 与 diff 引擎
- [x] `SyncManifest` / `SyncObjectMeta` / `SyncLocalSnapshot` / `SyncPlan`
- [x] `SyncEntityKeys` 统一键、`SyncTime` 时间转换
- [x] `SyncDiff` 纯函数（墓碑优先 → 对象 LWW/冲突）
- [x] `SyncJsonContext` 源生成序列化（含 manifest，防 Android 裁剪）
- [x] 测试项目 `tests/SwashbucklerDiary.Sync.Tests`，14 个用例全绿

### 阶段 1：同步基础设施
- [x] `SyncGraveModel`（墓碑表）+ `AddSqlsugar` 注册
- [x] `SyncGraveRepository`（固定主库）
- [x] `ISyncGraveService` / `SyncGraveService`
- [x] `SyncContext`（同步导入时抑制 AOP 改写 `UpdateTime`）
- [x] `SyncState` / `ISyncStateStore` / `SyncStateStore`
- [x] `ISyncStorage` 抽象 + DTO
- [x] `LocalSyncStorage`（本地目录实现，用于离线模拟双端）
- [x] `IWebDAV` 扩展：`GetFileAsync` / `PutFileAsync(ifMatch)` / `DeleteFileAsync` / `MakeCollectionAsync` / `GetETagAsync` / `ExistsAsync` / `ListAsync`
- [x] `WebDavSyncStorage`
- [x] `ISyncService` / `SyncService.ProbeAsync`（存储层 + AOP 自检）
- [x] AOP 兜底刷新 `UpdateTime`
- [x] DI 注册（Maui / Gtk）
- [x] DEBUG 自检入口：设置 → 备份 → WebDAV 备份 →「同步自检（DEBUG）」

## 本机构建产物（2026-11-15，含审查修复）

| 平台 | 路径 |
|---|---|
| Windows 目录 | `artifacts\diary-sync-win\SwashbucklerDiary.Maui.exe` |
| Windows zip | `artifacts\diary-sync-win.zip`（154 MB） |
| Android APK | `artifacts\diary-sync-android\SwashbucklerDiary-diary-sync-arm64.apk`（85 MB，自签名 `CN=DiarySync`） |

### 代码审查修复（P1 + P2）

- P1 路径穿越：`WebDavSyncStorage.Full` 拒绝 `..`、绝对路径、反斜杠
- P1 Android 裁剪：`SyncStateStore` 改用 `JsonSerializerContext`，异常放宽并记日志
- P1 Guid 主键：`SyncGraveService` 插入时补 `Id = Guid.NewGuid()`（审查时漏掉，修复中发现）
- P2：`If-Match` 添加失败抛异常；`LeaveOpenStreamContent` 不关闭调用方流；取消异常传播；
  `SemaphoreSlim` 串行化；`HttpClient` 2 分钟超时 + ct 贯穿；本地路径前缀带分隔符；
  `TouchAsync` 移到 `ISyncDiagnosticsService`

### 待复验

- [x] 真机自检「正确 If-Match 应成功」→ **HTTP 204 PASS**（2026-11-15 11:41，Android）
- [x] 真机自检第 1 项（AOP）→ **PASS**。实测 `OperationType.ToString()` 返回 `UpdateByObject`，
      已改为前缀匹配（`StartsWith("Update")` / `StartsWith("Add")`）
- [x] 真机自检其余 6 项全部 PASS

构建环境全部在 D 盘：

```
D:\Tools\dotnet          .NET SDK 10.0.404 + maui/Android workload
D:\Tools\jdk17           JDK 17
D:\Tools\android-sdk     Android SDK（build-tools 36 / platform android-36）
D:\Tools\nuget           NuGet 缓存
D:\Tools\diary-sync.keystore   自签名密钥（口令 diarysync123）
```

根目录快捷方式 `侠客日记.lnk` 已指向 `artifacts\diary-sync-win\SwashbucklerDiary.Maui.exe`。

> 注意：`ShowSyncProbe` 曾用 `#if DEBUG` 控制，导致 Release 包里入口被裁掉；已改为无条件显示，阶段 6 替换为正式同步页时删除。

## 待真机验证

- [ ] 自检第 1 项「UpdateTime 兜底（AOP）」是否 PASS
- [ ] 自检其余 7 项（目录、写入、读回、412、条件写、删除）是否 PASS
- [ ] Android / Windows 各跑一次

## 已知约束

1. ETag 不带引号，原样回传。
2. `PUT` 不返回 ETag，需要时再 `GET`（实现里用 `PROPFIND` 取）。
3. 无 `Last-Modified`，不能依赖服务器时间。
4. 单次 PUT ≈ 164 ms，首次同步走 zip 初始化。
5. `Updateable(entity).UpdateColumns(columns)` 若不含 `UpdateTime`，AOP 设置的值不会落库——这类调用点需显式带上 `UpdateTime`，阶段 3 前统一排查。
6. SqlSugar AOT 包裁掉了 `DataOperationType` 枚举，AOP 用 `OperationType.ToString()` 判断，已加自检。

## 未决问题

- 隐私模式库首版不同步，需在 UI 明示。
- 阶段 3 前需要扫描所有 `UpdateAsync(..., columns)` 与 `SetColumns(...)` 调用点，确认 `UpdateTime` 是否被写入。
