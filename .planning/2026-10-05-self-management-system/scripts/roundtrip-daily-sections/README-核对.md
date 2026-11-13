# 日课正文往返核对

验证 `DailyReviewSections` 的「正文 ↔ 字段」转换：新增/改动 `## 1.x` 章节时跑一遍。

```bash
dotnet run -c Release --project .planning/2026-10-05-self-management-system/scripts/roundtrip-daily-sections/roundtrip.csproj
```

期望最后一行 `RESULT=ALL OK`。覆盖：六节往返、1.4 的 `### 分块`不被打乱、
受管区块不被当成正文、老格式（1.1~1.5）向后兼容、只写 1.6 不选情绪、手改顺序不丢字。
