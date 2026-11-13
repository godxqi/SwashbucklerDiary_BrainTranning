# -*- coding: utf-8 -*-
"""2.1 结算提示那条砍短：把「环境原因进归档 / 自己的回 2.2」那两句去掉——
7.4 那条路已经断了，再把用户往那儿引就是错的。只留「写原因，不是写悔意」。
"""
import io
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))
I18N = os.path.join(ROOT, 'src/SwashbucklerDiary.Rcl/wwwroot/i18n')

OLD = ('Write the reason, not the regret. A reason that was the environment goes to the archive; '
       'one that was you goes back to 2.2.')
NEW = 'Write the reason, not the regret.'
ZH_NEW = '写原因，不是写悔意。'

for name in ('en-US.json', 'zh-CN.json'):
    path = os.path.join(I18N, name)
    lines = io.open(path, encoding='utf-8', newline='').read().split('\n')
    out, n = [], 0
    for line in lines:
        if '"%s":' % OLD in line:
            tail = ',' if line.rstrip().endswith(',') else ''
            out.append('  "%s": "%s"%s' % (NEW, ZH_NEW if name == 'zh-CN.json' else NEW, tail))
            n += 1
        else:
            out.append(line)
    io.open(path, 'w', encoding='utf-8', newline='').write('\n'.join(out))
    print('%-12s renamed=%d' % (name, n))

for name in ('en-US.json', 'zh-CN.json'):
    d = json.load(io.open(os.path.join(I18N, name), encoding='utf-8'))
    stale = [k for k in d if 'goes to the archive' in k]
    print('%-12s keys=%d | 旧键残留 %d | 新键在 %s' % (name, len(d), len(stale), NEW in d))
