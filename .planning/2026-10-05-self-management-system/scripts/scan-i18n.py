# -*- coding: utf-8 -*-
"""扫描阶段 3 新增/改动的文件里用到的 i18n key，和 en-US.json 对比，列出缺的。

只是开发时的核对工具，不参与构建。

**关键**：不能只匹配 `I18n.T("字面量")`。项目里大量写法是
`I18n.T(x switch { A => "Some key", ... })`——字面量不在 `I18n.T(` 的正后面。
第一版就是这么漏掉了 4 个芯片名（真机上显示成英文才发现的）。
所以这里改成：找到 `I18n.T(`，再按括号配对扫到它结束，把里面的所有字符串字面量都收进来。
"""
import io
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
I18N = os.path.join(ROOT, "src", "SwashbucklerDiary.Rcl", "wwwroot", "i18n", "en-US.json")

TARGET = os.path.join(ROOT, "src", "SwashbucklerDiary.Rcl")

FILES = [
    "Pages/Review/MonthlyReviewPage.razor",
    "Pages/Review/MonthlyReviewPage.razor.cs",
    "Pages/Review/LimitingBeliefEditDialog.razor",
    "Pages/Review/EnemyEditDialog.razor",
    "Pages/Review/AffirmationEditDialog.razor",
    "Pages/Review/AffirmationEditDialog.razor.cs",
    "Pages/Review/VirtuePage.razor",
    "Pages/Review/VirtuePage.razor.cs",
    "Pages/Review/AffirmationLibraryPage.razor",
    "Pages/Review/AffirmationLibraryPage.razor.cs",
    "Pages/Review/ArchivePage.razor",
    "Pages/Review/ArchivePage.razor.cs",
    "Pages/Review/KanbanPage.razor",
    "Pages/Review/WeeklyReviewPage.razor",
    "Pages/Review/WeeklyReviewPage.razor.cs",
    "Pages/Home/HomePage.razor",
    "Services/ReviewSchedule/ReviewSectionText.cs",
]

# 注意大小写：服务里的参数名叫 i18n，页面里是 I18n——第一版只匹配了大写，
# 结果 ReviewSectionText.MonthlyLabels 里的 key 全漏了（真机上 3.2 标题显示成英文才发现的）
CALL = re.compile(r"[Ii]18n\.T\(")
LITERAL = re.compile(r'"((?:[^"\\]|\\.)*)"')
CONST = re.compile(r'public\s+const\s+string\s+\w+\s*=\s*"((?:[^"\\]|\\.)*)"\s*;')


def unescape(value):
    return value.replace('\\"', '"').replace('\\\\', '\\')


def keys_in_arguments(text, start):
    """从 `I18n.T(` 后面开始，按括号配对找出这个调用的实参区间里的所有字符串字面量。"""
    depth = 1
    i = start
    while i < len(text) and depth > 0:
        ch = text[i]
        if ch == '"':
            # 跳过整个字符串字面量，里面的括号不算数
            j = i + 1
            while j < len(text) and text[j] != '"':
                j += 2 if text[j] == '\\' else 1
            i = j + 1
            continue
        if ch == '(':
            depth += 1
        elif ch == ')':
            depth -= 1
        i += 1

    arguments = text[start:i - 1]
    return [unescape(m.group(1)) for m in LITERAL.finditer(arguments)]


def collect():
    keys = {}
    for rel in FILES:
        path = os.path.join(TARGET, rel.replace("/", os.sep))
        if not os.path.exists(path):
            print("MISSING FILE: " + rel)
            continue
        with io.open(path, encoding="utf-8") as fh:
            text = fh.read()

        for match in CALL.finditer(text):
            for key in keys_in_arguments(text, match.end()):
                keys.setdefault(key, rel)

        if rel.endswith("ReviewSectionText.cs"):
            for match in CONST.finditer(text):
                keys.setdefault(unescape(match.group(1)), rel)

    return keys


def main():
    with io.open(I18N, encoding="utf-8") as fh:
        existing = set(json.load(fh).keys())

    keys = collect()
    missing = sorted(k for k in keys if k not in existing)

    print("used=%d existing-hit=%d missing=%d" % (len(keys), len(keys) - len(missing), len(missing)))
    print("-" * 60)
    for key in missing:
        print(key)
    return 0


if __name__ == "__main__":
    sys.exit(main())
