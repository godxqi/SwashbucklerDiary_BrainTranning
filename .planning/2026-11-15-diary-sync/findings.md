# 阶段 0 探测结果：坚果云 WebDAV 能力

**探测时间**：2026-11-15 10:29
**目标**：`https://dav.jianguoyun.com/dav/SwashbucklerDiary/`
**探测脚本**：`D:\Temp\jgy-probe\probe.ps1`（零依赖，PowerShell + HttpClient）
**原始输出**：`D:\Temp\jgy-probe\stage0-result.txt`

---

## 一、逐项结果

| 项 | 内容 | 结果 | 详情 |
|---|---|---|---|
| P1 | 根目录 `PROPFIND` | PASS | HTTP 207 |
| P2 | `SwashbucklerDiary/` | PASS | 已存在 |
| P3a–e | `Sync/`、`diaries/`、`review/`、`assets/`、`assets/Image/` | PASS | 逐级创建成功 |
| P4a | `PUT` 新建 | PASS | HTTP 204，响应**无 ETag** |
| P4b | `GET` 读回 | PASS | HTTP 200，ETag 可取得，**无 `Last-Modified`** |
| P4c | ETag 存在 | PASS | 形如 `TisiKPx-feuWwHcna9T80A`（**不带引号**） |
| P5 | `GET + If-None-Match` | PASS | HTTP 304 |
| P6 | `PUT + 错误 If-Match` | PASS | **HTTP 412** |
| P7 | `PUT + 正确 If-Match` | PASS | HTTP 204 |
| P8 | 连续 50 次 `PUT` | PASS | 50/50 成功，总 8180 ms，均值 **163.6 ms/次** |
| P9 | `PROPFIND Depth:1` | PASS | HTTP 207，6 个条目 |
| P10 | `DELETE` 与删除后 `GET` | PASS | 204 / 404 |
| — | 清理 | PASS | `.probe/` 与 `.probe.txt` 已删除 |

---

## 二、关键结论

### 1. ETag + `If-Match` 完整可用 → 并发控制走 CAS，不需要锁文件

P6 返回 412，P7 在正确 ETag 下成功覆盖。这确认了原计划的主路径可行：

- 同步开始时 `GET manifest.json`，同时取得 ETag；
- 所有内容文件上传完毕后，`PUT manifest.json` 带 `If-Match: <etag>`；
- 返回 412 说明另一台设备已提交，重新拉取 manifest 重试一轮。

**Task Plan 里的 Plan B（`sync.lock`）降级为「仅在坚果云策略变更时启用」，阶段 1 不实现。**

### 2. ETag 必须原样回传，不能按 RFC 加引号

坚果云返回的 ETag 是 `TisiKPx-feuWwHcna9T80A`，**没有双引号**。P7 传原值成功。
实现时把 `ETag` 响应头的值原样存进变量、原样放进 `If-Match`，不要做任何格式化。

### 3. `PUT` 不返回 ETag，`GET` 不返回 `Last-Modified`

两条实现约束：

- 写完文件后若需要 ETag，必须再 `GET` 一次（或用 `PROPFIND`）。
- 不能依赖服务器时间戳判断新旧——这印证了 manifest 里用对象自身 `UpdateTime` 的设计。

### 4. 单次 PUT 约 164 ms，首次全量同步需要优化

串行 50 次耗时 8.2 秒，折算：

| 规模 | 串行预估 |
|---|---|
| 1,000 篇日记 | 约 2.7 分钟 |
| 500 个附件 | 约 1.4 分钟 |

结论：**首次同步（云端为空）不逐文件推送，改用现有的 `ExportJsonAsync` 打一个 zip 上传**，一次请求完成初始化；之后的日常同步才走单文件增量。这条要写进阶段 3。

日常增量通常只有几篇日记，单文件完全够用。

### 5. 短时批量写入未被限流

50 次连续 PUT 无失败、无 429。阶段 5 附件同步可以采用**小并发（4–6）+ 重试**，但必须：

- 限制并发数，别按文件数放大；
- 对 429 / 5xx 做指数退避；
- 单文件失败不阻断整体同步，最后汇总。

---

## 三、对实施计划的影响

| 计划条目 | 调整 |
|---|---|
| 阶段 0 的 Plan B（`sync.lock`） | 不实现，保留文档 |
| 阶段 1 `ISyncStorage.PutFileAsync(ifMatch)` | 接口保留 `ifMatch` 参数，实现直接透传原值 |
| 阶段 2 manifest | 确认不依赖服务器时间，只用对象 `UpdateTime` |
| 阶段 3 首次同步 | 增加「云端为空时用 JSON zip 一次性上传」分支 |
| 阶段 5 附件 | 串行改小并发（4–6）+ 指数退避 |
| 阶段 6 UI | 首次同步要显示「正在初始化，可能需要几分钟」 |

---

## 四、未验证项（留到后续阶段）

| 项 | 何时验证 |
|---|---|
| `MKCOL` 已存在时的返回码（本次全部「已存在」，未触发创建分支） | 阶段 1 用新目录名验证 |
| 空间/流量超限时的错误码（403/507） | 阶段 5 附件同步 |
| 并发 PUT 的实际限流阈值 | 阶段 5 |
| 长路径、空目录、中文文件名 | 设计上全用 ASCII，可不测 |
| 大文件（>100 MB 视频）行为 | 阶段 5，单文件上限提示 |

---

## 五、当前远端状态

坚果云上已保留以下**空目录**（这是同步目录结构的一部分，不需要清理）：

```
SwashbucklerDiary/Sync/
SwashbucklerDiary/Sync/diaries/
SwashbucklerDiary/Sync/review/
SwashbucklerDiary/Sync/assets/
SwashbucklerDiary/Sync/assets/Image/
```

探测产生的临时文件已全部删除，现有备份 zip 未受影响。
