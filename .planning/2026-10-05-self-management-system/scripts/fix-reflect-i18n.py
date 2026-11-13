# -*- coding: utf-8 -*-
"""补一刀：结算追问那条键里带**转义引号**（\\"Nothing comes to mind\\"），
上一版脚本没转义所以没匹配到。这里按文件里的真实字面量匹配。

同时改 C# 那个 const —— 它才是这条文案的真正来源（razor 引用的是 const，不是字面量）。
"""
import io
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
I18N = os.path.join(ROOT, 'src/SwashbucklerDiary.Rcl/wwwroot/i18n')

# 注意：下面的 \\" 在 Python 里是「反斜杠 + 引号」，正是 JSON / C# 文件里的样子
OLD = ('This week, what happened differently because of this system? One sentence is enough. '
       '\\"Nothing comes to mind\\" is a valid answer.')
NEW = 'One sentence is enough. \\"Nothing comes to mind\\" is a valid answer.'
ZH_NEW = '一句话就够。答「想不出来」也是合格的答案。'


def patch_pack(name):
    path = os.path.join(I18N, name)
    lines = io.open(path, encoding='utf-8', newline='').read().split('\n')
    out, n = [], 0
    for line in lines:
        if '"%s":' % OLD in line:
            tail = ',' if line.rstrip().endswith(',') else ''
            value = ZH_NEW if name == 'zh-CN.json' else NEW
            out.append('  "%s": "%s"%s' % (NEW, value, tail))
            n += 1
        else:
            out.append(line)
    io.open(path, 'w', encoding='utf-8', newline='').write('\n'.join(out))
    print('%-12s renamed=%d' % (name, n))


def patch_const():
    path = os.path.join(ROOT, 'src/SwashbucklerDiary.Rcl/Services/ReviewSchedule/ReviewSectionText.cs')
    text = io.open(path, encoding='utf-8', newline='').read()
    old_line = 'public const string SettlementReflectionDescription = "%s";' % OLD
    new_line = 'public const string SettlementReflectionDescription = "%s";' % NEW
    if old_line not in text:
        print('C# const: 没找到（可能已改过）')
        return
    io.open(path, 'w', encoding='utf-8', newline='').write(text.replace(old_line, new_line))
    print('C# const 已改')


patch_pack('en-US.json')
patch_pack('zh-CN.json')
patch_const()

for name in ('en-US.json', 'zh-CN.json'):
    d = json.load(io.open(os.path.join(I18N, name), encoding='utf-8'))
    stale = [k for k in d if 'what happened differently' in k]
    fresh = [k for k in d if k.startswith('One sentence is enough')]
    print('%-12s keys=%d | 新键 %d | 旧键残留 %d' % (name, len(d), len(fresh), len(stale)))
