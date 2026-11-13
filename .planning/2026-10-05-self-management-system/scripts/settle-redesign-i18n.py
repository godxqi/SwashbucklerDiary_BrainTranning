# -*- coding: utf-8 -*-
"""2.1 结算重设计（2026-10-06）的 i18n：

1. 结算追问那条说明删掉重复的问句（小标题已经问过一次了）
2. 7.4 的入口说明删掉「来自 2.1：原因是环境」那半句（结算不再写 7.4）
3. 新增三条：归因提示 / 老目标没写 SMART / 勾了没写原因
"""
import io
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
I18N = os.path.join(ROOT, 'src/SwashbucklerDiary.Rcl/wwwroot/i18n')
CODE_FILES = [
    'src/SwashbucklerDiary.Rcl/Pages/Review/ArchivePage.razor.cs',
]

OLD_REFLECT = 'This week, what happened differently because of this system? One sentence is enough. "Nothing comes to mind" is a valid answer.'
NEW_REFLECT = 'One sentence is enough. "Nothing comes to mind" is a valid answer.'
ZH_REFLECT = '一句话就够。答「想不出来」也是合格的答案。'

OLD_EXIT = ('Reached from 2.1 (the reason was the environment, not you) and 2.2 '
            "(the stumbling block that lost this week's single slot). "
            'When the same entry shows up three times, it is not a one-off.')
NEW_EXIT = ("Reached from 2.2 (the stumbling block that lost this week's single slot). "
            'When the same entry shows up three times, it is not a one-off.')
ZH_EXIT = '入口是周课 2.2（本周落选的绊脚石）。同一条在这里累计出现 3 次，就说明它不是偶发。'

NEW_KEYS = {
    'en-US.json': {
        'Which SMART item is why it was not achieved? Check the ones that apply, then write the reason in the box that appears.':
            'Which SMART item is why it was not achieved? Check the ones that apply, then write the reason in the box that appears.',
        'This goal was set without SMART notes, so there is nothing to attribute.':
            'This goal was set without SMART notes, so there is nothing to attribute.',
        'Every checked item needs a reason.':
            'Every checked item needs a reason.',
    },
    'zh-CN.json': {
        'Which SMART item is why it was not achieved? Check the ones that apply, then write the reason in the box that appears.':
            '哪一项是没达成的原因？勾上适用的，然后在出现的框里写清为什么。',
        'This goal was set without SMART notes, so there is nothing to attribute.':
            '这个目标定的时候没写 SMART，没得归因。',
        'Every checked item needs a reason.':
            '勾了的项都要写原因。',
    },
}


def patch_json(name):
    path = os.path.join(I18N, name)
    text = io.open(path, encoding='utf-8', newline='').read()
    lines = text.split('\n')
    out = []
    renamed = 0

    for line in lines:
        stripped = line.rstrip().rstrip(',')
        if '"%s"' % OLD_REFLECT in stripped:
            tail = ',' if line.rstrip().endswith(',') else ''
            value = ZH_REFLECT if name == 'zh-CN.json' else NEW_REFLECT
            out.append('  "%s": "%s"%s' % (NEW_REFLECT, value, tail))
            renamed += 1
            continue
        if '"%s"' % OLD_EXIT in stripped:
            tail = ',' if line.rstrip().endswith(',') else ''
            value = ZH_EXIT if name == 'zh-CN.json' else NEW_EXIT
            out.append('  "%s": "%s"%s' % (NEW_EXIT, value, tail))
            renamed += 1
            continue
        out.append(line)

    body = '\n'.join(out).rstrip()
    assert body.endswith('}'), 'tail changed unexpectedly'
    head = body[:-1].rstrip()
    add = [u'  "%s": "%s"' % (k, v) for k, v in NEW_KEYS[name].items() if ('"%s":' % k) not in body]
    if add:
        body = head + u',\n' + u',\n'.join(add) + u'\n}'

    io.open(path, 'w', encoding='utf-8', newline='').write(body)
    print('%-12s renamed=%d added=%d' % (name, renamed, len(add)))


def patch_code():
    for rel in CODE_FILES:
        path = os.path.join(ROOT, rel)
        text = io.open(path, encoding='utf-8', newline='').read()
        if OLD_EXIT in text:
            io.open(path, 'w', encoding='utf-8', newline='').write(text.replace(OLD_EXIT, NEW_EXIT))
            print('  patched', rel)
        else:
            print('  unchanged', rel)


for name in ('en-US.json', 'zh-CN.json'):
    patch_json(name)
patch_code()

# 自检：两个包都必须能解析，且三条新键都在
for name in ('en-US.json', 'zh-CN.json'):
    d = json.load(io.open(os.path.join(I18N, name), encoding='utf-8'))
    missing = [k for k in NEW_KEYS[name] if k not in d]
    print('%-12s keys=%d missing=%s' % (name, len(d), missing or 'none'))
