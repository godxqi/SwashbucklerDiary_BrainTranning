# -*- coding: utf-8 -*-
"""真机库的只读体检：表清单 + 指定表的建表语句 + 行数。"""
import io
import os
import sqlite3
import sys

DATA = os.path.expanduser(
    "~/AppData/Local/Yu-Core/36234Yu-Core.12598F3457A93/Data")

TABLES = [
    "SelfAffirmationModel", "MonthlyReviewModel", "LimitingBeliefModel",
    "EnemyModel", "VirtueEntryModel", "ArchiveItemModel",
    "StumblingBlockModel", "WeeklyGoalModel", "WeeklyReviewModel",
]


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "SwashbucklerDiary.db3"
    path = os.path.join(DATA, which)
    con = sqlite3.connect(path)
    cur = con.cursor()

    names = [r[0] for r in cur.execute(
        "select name from sqlite_master where type='table' order by name")]
    print("tables=%d" % len(names))

    for table in TABLES:
        if table not in names:
            print("MISSING TABLE: %s" % table)
            continue
        cols = [r[1] for r in cur.execute("pragma table_info(%s)" % table)]
        count = list(cur.execute("select count(*) from %s" % table))[0][0]
        print("%-24s rows=%-4d cols=%s" % (table, count, ",".join(cols)))

    print("-" * 60)
    if "SelfAffirmationModel" in names:
        sql = list(cur.execute(
            "select sql from sqlite_master where name='SelfAffirmationModel'"))[0][0]
        print(sql)
    con.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
