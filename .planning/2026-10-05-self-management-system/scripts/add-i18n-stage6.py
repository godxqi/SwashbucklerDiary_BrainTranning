"""阶段 6 追加 i18n 词条（补足 / 随时改动过去复盘）。

只动两个语言包的最后一段：在收尾的 `}` 之前把新键插进去，
保持原有的缩进、LF 行尾、无尾随换行、无 BOM —— 不用 json 模块重排整个文件，
否则会被 reformat 成全量重写（diff 里全是噪声，还容易改错转义）。
"""
import io
import sys

ROOT = 'src/SwashbucklerDiary.Rcl/wwwroot/i18n/'

EN = {
    "This week's weekly review": "This week's weekly review",
    "This month's monthly review": "This month's monthly review",
    "Delete this daily review": "Delete this daily review",
    "Skipped": "Skipped",
    "When it happened": "When it happened",
    "Skip this week": "Skip this week",
    "Unskip this week": "Unskip this week",
    "Skip this month": "Skip this month",
    "Unskip this month": "Unskip this month",
    "Nothing written for this day yet. Filling it in will create a new entry.":
        "Nothing written for this day yet. Filling it in will create a new entry.",
    "This deletes the diary for that day as well, along with anything the weekly review decided about its micro failures.":
        "This deletes the diary for that day as well, along with anything the weekly review decided about its micro failures.",
    "No daily review was written for this week - the list below and the candidate pool both come from the daily reviews.":
        "No daily review was written for this week - the list below and the candidate pool both come from the daily reviews.",
    "This week is skipped - it will not count as owed in the stats. Writing anything here cancels the skip.":
        "This week is skipped - it will not count as owed in the stats. Writing anything here cancels the skip.",
    "This month is skipped - it will not count as owed in the stats. Writing anything here cancels the skip.":
        "This month is skipped - it will not count as owed in the stats. Writing anything here cancels the skip.",
    "New entries are recorded on this date. Backfilling? Pick the day they belong to.":
        "New entries are recorded on this date. Backfilling? Pick the day they belong to.",
    "It defaults to now. Backfilling something from before? Pick the day it actually happened.":
        "It defaults to now. Backfilling something from before? Pick the day it actually happened.",
}

ZH = {
    "This week's weekly review": "这一周（周课）",
    "This month's monthly review": "这一个月（月课）",
    "Delete this daily review": "删除这次日课",
    "Skipped": "已跳过",
    "When it happened": "发生时间",
    "Skip this week": "跳过这一周",
    "Unskip this week": "撤销跳过这一周",
    "Skip this month": "跳过这一月",
    "Unskip this month": "撤销跳过这一月",
    "Nothing written for this day yet. Filling it in will create a new entry.":
        "这天还没写过。现在开始填写会新建一篇。",
    "This deletes the diary for that day as well, along with anything the weekly review decided about its micro failures.":
        "这会连那一天的日记一起删除；周课里对这些微观失败的判定也会一并删掉。",
    "No daily review was written for this week - the list below and the candidate pool both come from the daily reviews.":
        "这一周一条日课都没写——下面的清单和候选池都是从日课里长出来的。",
    "This week is skipped - it will not count as owed in the stats. Writing anything here cancels the skip.":
        "这一周已跳过，统计里不算欠。在这里写任何东西都会自动取消跳过。",
    "This month is skipped - it will not count as owed in the stats. Writing anything here cancels the skip.":
        "这一月已跳过，统计里不算欠。在这里写任何东西都会自动取消跳过。",
    "New entries are recorded on this date. Backfilling? Pick the day they belong to.":
        "新记的几条会落在这个日期上。补过去的就选它们真正属于的那一天。",
    "It defaults to now. Backfilling something from before? Pick the day it actually happened.":
        "默认是现在。要补过去的事，就选它真正发生的那一天。",
}


def escape(s):
    # 键与值里目前只有 '-' 和 '?'，没有引号与反斜杠；照样转义一遍，将来加了也不会炸
    return s.replace('\\', '\\\\').replace('"', '\\"')


def add(path, pairs):
    with io.open(path, 'r', encoding='utf-8', newline='') as f:
        text = f.read()

    if '\r' in text:
        sys.exit('unexpected CR in ' + path)

    # 幂等：已经加过的键跳过并报告
    added = []
    skipped = []
    for key in pairs:
        if u'"%s":' % escape(key) in text:
            skipped.append(key)
        else:
            added.append(key)

    if not added:
        print('%-14s nothing to add (%d already there)' % (path.split('/')[-1], len(skipped)))
        return

    idx = text.rindex('}')
    head = text[:idx].rstrip()
    if not head.endswith(('"', '}', ']')):
        sys.exit('unexpected tail before }: %r' % head[-20:])

    lines = [u'  "%s": "%s"' % (escape(k), escape(pairs[k])) for k in added]
    new_text = head + u',\n' + u',\n'.join(lines) + u'\n' + text[idx:]

    with io.open(path, 'w', encoding='utf-8', newline='') as f:
        f.write(new_text)

    print('%-14s +%d keys (%d already there, %d bytes -> %d bytes)'
          % (path.split('/')[-1], len(added), len(skipped), len(text), len(new_text)))


add(ROOT + 'en-US.json', EN)
add(ROOT + 'zh-CN.json', ZH)
