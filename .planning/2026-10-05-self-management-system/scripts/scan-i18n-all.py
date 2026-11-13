# -*- coding: utf-8 -*-
"""全量扫 i18n：把 Rcl 下所有 .razor / .cs 用到的 key 和 13 个语言包对比。

阶段 3 的第一版脚本连漏两批（见 findings「i18n 漏键扫描的两个坑」），所以这里照修好后的口径写：

1. 不能只匹配 `I18n.T("字面量")`。项目里大量写法是
   `I18n.T(x switch { A => "Some key", ... })`——字面量不在 `I18n.T(` 正后面。
   → 找到 `[Ii]18n\.T\(`，按括号配对扫到实参结束，把区间里所有字符串字面量都收进来。
2. 服务里的参数名是小写 `i18n`，所以正则必须同时认大小写。

用法：
    python scan-i18n-all.py            # 列出所有语言包缺哪些 key
    python scan-i18n-all.py --count    # 只报每个语言包缺多少条
"""
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
RCL = os.path.join(ROOT, "src", "SwashbucklerDiary.Rcl")
I18N_DIR = os.path.join(RCL, "wwwroot", "i18n")

T_CALL = re.compile(r"[Ii]18n\.T\(")
STRING_LIT = re.compile(r'"((?:[^"\\]|\\.)*)"')

# C# 字符串里的转义要还原成真实字符，否则含引号的 key（比如
# `"...\"I scroll until 1am\"..."`）会带着反斜杠去比对，永远对不上。
UNESCAPE = {"n": "\n", "r": "\r", "t": "\t", "\\": "\\", '"': '"', "0": "\0"}


def unescape(raw):
    out = []
    i = 0
    while i < len(raw):
        ch = raw[i]
        if ch == "\\" and i + 1 < len(raw):
            nxt = raw[i + 1]
            out.append(UNESCAPE.get(nxt, "\\" + nxt))
            i += 2
        else:
            out.append(ch)
            i += 1
    return "".join(out)


def extract_keys(text):
    keys = set()
    for m in T_CALL.finditer(text):
        i = m.end()          # 指着 '(' 之后
        depth = 1
        j = i
        in_str = False
        esc = False
        while j < len(text) and depth > 0:
            ch = text[j]
            if in_str:
                if esc:
                    esc = False
                elif ch == "\\":
                    esc = True
                elif ch == '"':
                    in_str = False
            else:
                if ch == '"':
                    in_str = True
                elif ch == "(":
                    depth += 1
                elif ch == ")":
                    depth -= 1
            j += 1
        arg = text[i:j]
        for lit in STRING_LIT.finditer(arg):
            value = unescape(lit.group(1))
            # 过滤明显的非 key：拼接进去的插值片段、markdown 链接碎片
            if not value or value.startswith("[{") or "](" in value:
                continue
            keys.add(value)
    return keys


# 第三个坑（findings 里记过）：文案集中类的 key 是 `const string` 或数组里的字面量，
# 调用点是 `I18n.T(常量名)`——不是 `I18n.T(` 的实参，上面那套扫描一个都收不到。
# 所以这些「文案持有者」要单独扫一遍它们文件里的所有字符串字面量。
TEXT_HOLDER_FILES = [
    "src/SwashbucklerDiary.Rcl/Services/ReviewSchedule/ReviewSectionText.cs",
    "src/SwashbucklerDiary.Rcl/Pages/Review/StateRecordEditPage.razor.cs",
    "src/SwashbucklerDiary.Rcl/Pages/Review/InvokedPickerDialog.razor.cs",
    "src/SwashbucklerDiary.Shared/Utilities/Review/DailyReviewSections.cs",
    "src/SwashbucklerDiary.Shared/Utilities/Review/WeeklyReviewSections.cs",
    "src/SwashbucklerDiary.Shared/Utilities/Review/MonthlyReviewSections.cs",
    "src/SwashbucklerDiary.Shared/Utilities/Review/ReviewSections.cs",
]

# 明显的非 key：正则、markdown 标题、格式串、路径、CSS 类名
NOT_A_KEY = re.compile(r"[\#\\%]|://|^\s*$|^[a-z0-9_-]+$|^review-|^mdi:|^yyyy")


def looks_like_key(value):
    if len(value) < 3 or NOT_A_KEY.search(value):
        return False
    # key 是英文句子：含空格、首字母大写（或插值占位）
    return " " in value and (value[0].isupper() or value[0] == "{")


def collect_text_holder_keys():
    keys = set()
    for rel in TEXT_HOLDER_FILES:
        path = os.path.join(ROOT, rel)
        if not os.path.exists(path):
            continue
        with io.open(path, encoding="utf-8", errors="replace") as f:
            text = f.read()
        for lit in STRING_LIT.finditer(text):
            value = unescape(lit.group(1))
            if looks_like_key(value):
                keys.add(value)
    return keys


# 还有一类：key 放在数组/元组里（比如统计页的时间段芯片 `("Last 30 days", 30)`），
# 调用点是 `I18n.T(periodKeys[index])`。统一在「UI 文案会出现的那几个目录」里
# 按「像英文句子」的规则捞一遍，宁可多捞几条人工过一眼。
KEY_SCAN_DIRS = [
    "src/SwashbucklerDiary.Rcl/Pages",
    "src/SwashbucklerDiary.Rcl/Services",
    "src/SwashbucklerDiary.Rcl/Components",
    "src/SwashbucklerDiary.Shared/Utilities",
]


def collect_loose_keys():
    keys = set()
    for rel in KEY_SCAN_DIRS:
        base = os.path.join(ROOT, rel)
        for dirpath, _dirnames, filenames in os.walk(base):
            for name in filenames:
                if not name.endswith((".razor", ".cs")) or name.endswith(".backup.claude"):
                    continue
                with io.open(os.path.join(dirpath, name), encoding="utf-8", errors="replace") as f:
                    text = f.read()
                for lit in STRING_LIT.finditer(text):
                    value = unescape(lit.group(1))
                    if looks_like_key(value):
                        keys.add(value)
    return keys


# 手工列的「扫描器抓不到」的 key：它们放在数组/元组里，调用点是 `I18n.T(periodKeys[i])`，
# 既不是 `I18n.T(` 的实参，也不在文案类里。新增这类 key 时要记得补到这里。
EXTRA_KEYS = [
    "Last 30 days",
    "Last 90 days",
    "Last year",
    "This month",
    "Achievement growth",
    "Pick one",
]


def collect_source_keys():
    keys = set()
    for dirpath, _dirnames, filenames in os.walk(RCL):
        if "_framework" in dirpath or "npm" in dirpath:
            continue
        for name in filenames:
            if name.endswith((".razor", ".cs")) and not name.endswith(".backup.claude"):
                path = os.path.join(dirpath, name)
                with io.open(path, encoding="utf-8", errors="replace") as f:
                    keys |= extract_keys(f.read())
    keys |= collect_text_holder_keys()
    keys |= set(EXTRA_KEYS)
    return keys


def main():
    only_count = "--count" in sys.argv
    keys = collect_source_keys()
    print("源码里用到 %d 个 key\n" % len(keys))

    packs = sorted(n for n in os.listdir(I18N_DIR) if n.endswith(".json"))
    worst = 0
    for pack in packs:
        with io.open(os.path.join(I18N_DIR, pack), encoding="utf-8") as f:
            data = json.load(f)
        missing = sorted(keys - set(data))
        worst = max(worst, len(missing))
        if only_count:
            print("%-12s 共 %4d 条，缺 %3d 条" % (pack, len(data), len(missing)))
        else:
            print("=== %s 缺 %d 条 ===" % (pack, len(missing)))
            for k in missing:
                print("   " + k)
    print("\n最大缺口：%d 条" % worst)


if __name__ == "__main__":
    main()
