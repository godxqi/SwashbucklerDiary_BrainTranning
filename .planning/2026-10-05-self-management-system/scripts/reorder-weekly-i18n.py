# -*- coding: utf-8 -*-
"""周课改序（2026-10-06）的 i18n 联动改名。

旧 → 新 章节号是一组**轮换**：
    2.1 数频率      → 2.2
    2.2 提炼        → 2.3
    2.3 每周三目标  → 2.6
    2.4 上周结算    → 2.1
    2.5 四处出口    → 2.4
    2.6 新事物      → 2.5

凡是**键名里带章节号**的都必须连键一起改（键就是查找 id），
所以脚本要同时改语言包和源码里的字面量。改完必须跑扫描确认没有漏键。
"""
import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
I18N = os.path.join(ROOT, 'src/SwashbucklerDiary.Rcl/wwwroot/i18n')

# 旧编号 → 新编号（一次通过，不做顺序替换，免得 2.1→2.2 又被下一轮改成 2.3）
SECTION_MAP = {'2.1': '2.2', '2.2': '2.3', '2.3': '2.6', '2.4': '2.1', '2.5': '2.4', '2.6': '2.5'}

def remap(text):
    return re.sub(r'\b2\.[1-6]\b', lambda m: SECTION_MAP[m.group(0)], text)

# 键名里含章节号、或语义变了要改名的键（旧键 → 新键）
RENAME_KEYS = [
    "No candidate yet. Mark something as a stumbling block in 2.1 first.",
    "Back into this week's 2.1",
    "Fed back into this week's 2.1",
    "Feed back into this week's 2.1",
    "From 2.1: rejected this week",
    "From 2.4: the environment, not me",
    "Reached from 2.4 (the reason was the environment, not you) and 2.1 (the stumbling block that lost this week's single slot). When the same entry shows up three times, it is not a one-off.",
    "It keeps coming back. Send it into this week's 2.1 as a fresh undecided failure.",
    "It will be marked as fed back and a fresh undecided failure will be created in this week's 2.1.",
    "The four lists are not a to-do list. They are an archive, and only the fourth exit question in 2.5 writes into them.",
    "No goal was set last week",
]

# 语义改名（不带编号）：键 → 新键
SEMANTIC = {
    "No goal was set last week": "No goal was set for this week",
}

# 只有值里有编号、键里没有的
VALUE_ONLY = [
    "Micro failure intro",
    "Where the weekly goals that carried over past their week end up.",
]

# 改掉的旧标题键（删掉），以及新标题键（加上）
DROP_KEYS = [
    "Count the frequency",
    "Pick at most one",
    "Three weekly goals",
    "Settle last week's goals",
    "Four exits check",
]

NEW_KEYS = {
    "en-US.json": {
        "Count the micro failures": "Count the micro failures",
        "Distill the micro failures": "Distill the micro failures",
        "Next week's three goals": "Next week's three goals",
        "Settle this week's three goals": "Settle this week's three goals",
        "Check the four record tables": "Check the four record tables",
        "No goal was set for this week": "No goal was set for this week",
    },
    "zh-CN.json": {
        "Count the micro failures": "数微观失败频率",
        "Distill the micro failures": "微观失败提炼",
        "Next week's three goals": "下周三个目标",
        "Settle this week's three goals": "本周三个目标结算",
        "Check the four record tables": "四个记录表检查",
        "No goal was set for this week": "这周还没定目标",
    },
}

# 这些键的值里有编号，但键名干脆一起改；中文值同样要把编号换掉
ZH_VALUE_RENAME = {
    "No goal was set last week": "这周还没定目标",
}


def new_key_of(key):
    if key in SEMANTIC:
        return SEMANTIC[key]
    return remap(key)


def process_pack(name):
    path = os.path.join(I18N, name)
    text = io.open(path, encoding='utf-8', newline='').read()
    lines = text.split('\n')

    out = []
    renamed = 0
    value_fixed = 0
    dropped = 0

    for line in lines:
        m = re.match(r'^(\s*)"((?:[^"\\]|\\.)*)":\s*(.*)$', line)
        if not m:
            out.append(line)
            continue
        indent, key, rest = m.group(1), m.group(2), m.group(3)

        if key in DROP_KEYS:
            dropped += 1
            continue

        new_key = new_key_of(key)
        if new_key != key or key in VALUE_ONLY:
            # 值本身也要跟着换编号（zh 里是中文，编号一样）
            new_rest = remap(rest)
            # 行尾那个逗号必须原样留住，否则 JSON 就断了
            comma = ',' if rest.rstrip().endswith(',') else ''
            if key in ZH_VALUE_RENAME and name == 'zh-CN.json':
                new_rest = '"%s"%s' % (ZH_VALUE_RENAME[key], comma)
            if key in SEMANTIC and name == 'en-US.json':
                # en 的约定是键==值，语义改名时值也要跟着改，否则键说「这周」值还说「上周」
                new_rest = '"%s"%s' % (SEMANTIC[key], comma)
            if new_key != key:
                renamed += 1
            else:
                value_fixed += 1
            out.append(f'{indent}"{new_key}": {new_rest}')
            continue

        out.append(line)

    body = '\n'.join(out).rstrip()
    assert body.endswith('}'), 'tail changed unexpectedly'

    additions = NEW_KEYS[name]
    add_lines = [u'  "%s": "%s"' % (k, v) for k, v in additions.items() if ('"%s":' % k) not in body]
    head = body[:-1].rstrip()
    body = head + u',\n' + u',\n'.join(add_lines) + u'\n}' if add_lines else body

    io.open(path, 'w', encoding='utf-8', newline='').write(body)
    print('%-12s renamed=%d value_fixed=%d dropped=%d added=%d'
          % (name, renamed, value_fixed, dropped, len(add_lines)))


CODE_FILES = [
    'src/SwashbucklerDiary.Rcl/Pages/Review/ArchivePage.razor',
    'src/SwashbucklerDiary.Rcl/Pages/Review/ArchivePage.razor.cs',
    'src/SwashbucklerDiary.Rcl/Pages/Review/KanbanPage.razor',
    'src/SwashbucklerDiary.Rcl/Pages/Review/WeeklyReviewPage.razor',
]


def process_code():
    for rel in CODE_FILES:
        path = os.path.join(ROOT, rel)
        text = io.open(path, encoding='utf-8', newline='').read()
        original = text
        for key in RENAME_KEYS:
            new = new_key_of(key)
            if new != key and key in text:
                text = text.replace(key, new)
        if text != original:
            io.open(path, 'w', encoding='utf-8', newline='').write(text)
            print('  patched', rel)
        else:
            print('  unchanged', rel)


process_pack('en-US.json')
process_pack('zh-CN.json')
process_code()
