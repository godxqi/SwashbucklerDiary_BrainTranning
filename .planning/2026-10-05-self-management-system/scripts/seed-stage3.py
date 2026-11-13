# -*- coding: utf-8 -*-
"""往真机库里灌一批阶段 3 的演示数据，用来真机验证页面渲染与闸门判定。

- `seed`   灌数据
- `clean`  按固定 Id 把本脚本灌进去的行清掉
只动复盘那几张表；触因标签是新建的，不碰用户原有数据。
一律带列名插入——真机表的物理列顺序和模型属性顺序不一致（OrderIndex 排在最后），
位置插入会悄悄写错列。
"""
import os
import sqlite3
import sys
from datetime import date, timedelta

DATA = os.path.expanduser(
    "~/AppData/Local/Yu-Core/36234Yu-Core.12598F3457A93/Data")
DB = os.path.join(DATA, "SwashbucklerDiary.db3")

# 固定前缀 + 序号。Guid 末段必须是 12 位十六进制——
# 上一版写成 PREFIX+ "6%02d" 得到 13 位，SqlSugar 读回来时 Id 绑定直接报错。
MARK = "5eed0000-0000-4000-8000-"


def gid(suffix):
    return MARK + ("%012d" % suffix)


MONTH_REVIEW = gid(1)
BELIEF_1, BELIEF_2 = gid(11), gid(12)
ENEMY_1 = gid(21)
AFF_1, AFF_2 = gid(31), gid(32)
ARCHIVE_1, ARCHIVE_2 = gid(41), gid(42)
TRIGGER = gid(51)
VIRTUE_1, VIRTUE_2 = gid(61), gid(62)
BLOCKS = [gid(600 + i) for i in range(1, 7)]
STATES = [gid(700 + i) for i in range(1, 4)]
GOAL_WEEK, GOAL_1 = gid(81), gid(82)

NOW = "2026-10-05 22:40:00"
TODAY = date(2026, 10, 5)


def insert(cur, table, row):
    marks = ",".join("?" * len(row))
    cols = ",".join('"%s"' % c for c in row)
    cur.execute("insert into %s (%s) values (%s)" % (table, cols, marks),
                list(row.values()))


def seed(cur):
    # ——— 月课 2026/10：3.1 两条观念（一条有反面证据）、3.2 两条短语、3.4 一行敌人 ———
    insert(cur, "MonthlyReviewModel", {
        "Id": MONTH_REVIEW, "Year": 2026, "Month": 10, "DiaryId": None,
        "Step31Done": 1, "Step32Done": 1, "Step33Done": 0, "Step34Done": 0,
        "Step33Note": None, "IsFinalized": 0, "FinalizedTime": None,
        "CreateTime": NOW, "UpdateTime": NOW})

    insert(cur, "LimitingBeliefModel", {
        "Id": BELIEF_1, "MonthlyReviewId": MONTH_REVIEW,
        "Assertion": "我并不是一个能坚持下来的人",
        "SupportingEvidence": "过去三次计划都在两周内停了",
        "CounterEvidence": "去年那门课我连着上了四个月；上个月的日课也没断",
        "Utility": "它让我不敢定任何超过两周的计划",
        "CreateTime": NOW, "UpdateTime": NOW})
    insert(cur, "LimitingBeliefModel", {
        "Id": BELIEF_2, "MonthlyReviewId": MONTH_REVIEW,
        "Assertion": "我不会拒绝别人",
        "SupportingEvidence": None, "CounterEvidence": None, "Utility": None,
        "CreateTime": NOW, "UpdateTime": NOW})

    insert(cur, "SelfAffirmationModel", {
        "Id": AFF_1, "Content": "我已经完成过需要坚持四个月的事。",
        "Source": "3.1 的反面证据", "RepeatCount": 0, "LastRepeatedTime": None,
        "IsActive": 1, "CreateTime": NOW, "UpdateTime": NOW,
        "SourceMonthlyReviewId": MONTH_REVIEW})
    insert(cur, "SelfAffirmationModel", {
        "Id": AFF_2, "Content": "说不也是一种照顾关系的方式。",
        "Source": None, "RepeatCount": 3, "LastRepeatedTime": NOW,
        "IsActive": 1, "CreateTime": NOW, "UpdateTime": NOW,
        "SourceMonthlyReviewId": MONTH_REVIEW})

    insert(cur, "EnemyModel", {
        "Id": ENEMY_1, "MonthlyReviewId": MONTH_REVIEW,
        "Goal": "把身体练回来", "BlockingBehavior": "我每天晚上刷手机到一点",
        "DeepBelief": "觉得自己不配拥有更有精力的生活",
        "WeeklyAction": "这周三次把手机放在客厅充电再进卧室",
        "CreateTime": NOW, "UpdateTime": NOW})

    # ——— 归档区 7.4：一条累计 3 次（该回流了）、一条只有 1 次 ———
    insert(cur, "ArchiveItemModel", {
        "Id": ARCHIVE_1, "Kind": 3, "Source": 1,
        "Content": "答应了不该答应的活，结果自己熬夜", "OccurrenceCount": 3,
        "LastOccurredTime": "2026-09-28 20:00:00", "SourceWeeklyReviewId": None,
        "FedBackToWeekOfYear": None, "FedBackYear": None,
        "CreateTime": NOW, "UpdateTime": NOW})
    insert(cur, "ArchiveItemModel", {
        "Id": ARCHIVE_2, "Kind": 3, "Source": 2,
        "Content": "这一周也没打开过那本书", "OccurrenceCount": 1,
        "LastOccurredTime": "2026-10-03 20:00:00", "SourceWeeklyReviewId": None,
        "FedBackToWeekOfYear": None, "FedBackYear": None,
        "CreateTime": NOW, "UpdateTime": NOW})

    # ——— 触因：一个标签 + 3 次记录（才够 3 次，该交给月课 3.1）———
    insert(cur, "TagModel", {
        "Id": TRIGGER, "Name": "被否定", "CreateTime": NOW, "UpdateTime": NOW,
        "Kind": 1})
    for index, state_id in enumerate(STATES, start=1):
        insert(cur, "StateRecordModel", {
            "Id": state_id, "OccurredTime": "2026-10-0%d 21:00:00" % index,
            "Scene": None, "TriggerTagId": TRIGGER, "Body": None, "Mind": None,
            "Emotion": None, "Spirit": None, "EmotionName": None,
            "InvokedKind": None, "InvokedId": None, "ActionNow": None,
            "CreateTime": NOW, "UpdateTime": NOW})

    # ——— 优点表：成就列表两条（看增长小计）、感恩列表一条 ———
    insert(cur, "VirtueEntryModel", {
        "Id": VIRTUE_1, "Kind": 2, "Content": "把那篇拖了很久的稿子交了",
        "Date": "2026-09-20 00:00:00", "SourceDiaryId": None, "IsAchieved": 1,
        "AchievedTime": "2026-09-20 00:00:00", "CreateTime": NOW, "UpdateTime": NOW})
    insert(cur, "VirtueEntryModel", {
        "Id": VIRTUE_2, "Kind": 2, "Content": "开始每天走二十分钟",
        "Date": "2026-10-02 00:00:00", "SourceDiaryId": None, "IsAchieved": 1,
        "AchievedTime": "2026-10-02 00:00:00", "CreateTime": NOW, "UpdateTime": NOW})

    # ——— 本月微观失败：刷手机 ×3、拖延 ×2、未归类 ×1 ———
    year, week, _ = TODAY.isocalendar()
    blocks = [("刷手机", "又刷到一点"), ("刷手机", "开会时在刷"),
              ("刷手机", "睡前刷了四十分钟"), ("拖延", "把报告拖到最后一晚"),
              ("拖延", "约好的电话没打"), (None, "忘了回那条消息")]
    for index, (category, desc) in enumerate(blocks, start=1):
        insert(cur, "StumblingBlockModel", {
            "Id": BLOCKS[index - 1], "Year": year, "WeekOfYear": week,
            "Category": category, "Description": desc, "SourceDiaryId": None,
            "Verdict": 0, "CreateTime": NOW, "UpdateTime": NOW,
            "OrderIndex": index - 1})

    # ——— 7.2 的自动入口：上周定下、跨周还没做的目标 ———
    week_start = TODAY - timedelta(days=TODAY.weekday())
    last_start = week_start - timedelta(days=7)
    last_end = last_start + timedelta(days=6)
    ly, lw, _ = last_start.isocalendar()
    insert(cur, "WeeklyReviewModel", {
        "Id": GOAL_WEEK, "Year": ly, "WeekOfYear": lw,
        "StartDate": str(last_start) + " 00:00:00",
        "EndDate": str(last_end) + " 00:00:00", "DiaryId": None,
        "FrequencyConfirmed": 1, "GoalsConfirmed": 1, "IsFinalized": 1,
        "FinalizedTime": NOW, "CreateTime": NOW, "UpdateTime": NOW})
    insert(cur, "WeeklyGoalModel", {
        "Id": GOAL_1, "WeeklyReviewId": GOAL_WEEK, "Domain": 2,
        "Content": "把那本书读完前三章", "MacroGoalId": None,
        "SmartSpecific": None, "SmartMeasurable": None, "SmartAchievable": None,
        "SmartRelevant": None, "SmartTimeBound": None, "Status": 1,
        "SettledWeekOfYear": None, "SettledYear": None,
        "CreateTime": NOW, "UpdateTime": NOW, "SettlementNote": None})

    print("seeded  week=%d-W%02d  lastWeek=%d-W%02d" % (year, week, ly, lw))


def clean(cur):
    # 按前缀删，不按具体 Id：这样可以连上一版写坏的 37 位 Guid 一起清掉
    for table in ["MonthlyReviewModel", "LimitingBeliefModel", "EnemyModel",
                  "SelfAffirmationModel", "ArchiveItemModel", "VirtueEntryModel",
                  "StumblingBlockModel", "StateRecordModel",
                  "WeeklyGoalModel", "WeeklyReviewModel", "TagModel"]:
        removed = cur.execute("delete from %s where Id like ?" % table,
                              (MARK + "%",)).rowcount
        if removed:
            print("  %s -%d" % (table, removed))

    print("cleaned")


def main():
    action = sys.argv[1] if len(sys.argv) > 1 else "seed"
    con = sqlite3.connect(DB)
    cur = con.cursor()
    seed(cur) if action != "clean" else clean(cur)
    con.commit()
    con.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
