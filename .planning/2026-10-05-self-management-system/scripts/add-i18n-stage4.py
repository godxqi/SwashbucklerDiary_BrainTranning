# -*- coding: utf-8 -*-
"""把阶段 4 新增的 i18n key 追加进 zh-CN / en-US 语言包。

**只补这两个**（2026-10-05 主人拍板）：缺的 key 不会让界面崩，只是回退显示英文原句；
另外 11 个语言包（含 zh-TW）各缺 204 条，其中 55 条是方法论长句——那些我翻完也校验不了，
宁可让它们显示英文原句，也不要留下看着像翻译、实际走样的文案。

沿用阶段 3 脚本的两个口径：
  * 追加而不是重排——解析再整体序列化会把整个文件重排一遍，diff 全是噪音。
  * en-US 的值就是 key 本身（项目一贯做法）。
"""
import io
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
I18N_DIR = os.path.join(ROOT, "src", "SwashbucklerDiary.Rcl", "wwwroot", "i18n")

ZH = {
    # —— 状态管理表（方法文档 5）——
    "State record": "状态管理表",
    "Current situation": "当前场景",
    "What is going on right now.": "现在发生的是什么。",
    "Add trigger": "新建触因",
    "How it feels right now": "当前感受",
    "Physical": "身体上的",
    "Physical sensations.": "身体上的感觉。",
    "Mental": "心理上的",
    "What you are thinking.": "你在想什么。",
    "Emotional": "情感上的",
    "What you feel.": "你的感受。",
    "Spiritual": "精神上的",
    "Deep sense of meaning, purpose, and your place in the world.":
        "内心深处的意义感、使命感和在这个世界上的位置感。",
    "What is this emotion called": "这条情绪叫什么名字",
    "Emotional literacy. Naming it keeps some objectivity in play, which makes it far less likely to sweep you away. Pick one of the eight.":
        "情绪素养。给它命名能让你保持一点客观性，于是就不那么容易陷进去。从八大情绪里挑一个词。",
    "What to call on": "现在要调用哪一条",
    "Which entry you actually turned to (a virtue table, the affirmation library, or a macro goal). This is the most important cell in the whole table: these tables were built for exactly this moment. The book's instruction: once a limiting belief resurfaces, remind yourself that you have already weakened its influence. If nothing is called on here, they are just a collection.":
        "你实际翻到并调用的那一条（优点表 / 自我肯定库 / 宏观目标）。这一格是全表最重要的一格：这些表就是为这一刻建的。原文的要求是——一旦自我限制的观念又在脑海中浮现，请提醒自己，你已削弱了其消极影响。不在这里调用，它们就只是收藏。",
    "What to do now": "现在需要做什么",
    "The lowest-barrier action that moves you from a bad state back toward your source: sensory pleasure, the STOP exercise, or the fallback - going to bed.":
        "从糟糕状态靠向根源的最低门槛动作：感官的愉悦、「停止」练习，或者兜底方案——上床睡觉。",
    "The point of this table: when you are in a bad state, write down what you feel and what you need to do to move yourself back toward your source. It is not part of the daily rhythm - record it whenever it happens.":
        "这张表的用途：自身进入糟糕的状态时，记录下当前感受，以及在该状态下需要做什么事来促进自己靠向根源。它不属于每日节拍——随时可记。",
    "The trigger: the specific thing, scene, or person in the moment that feels familiar, and the clue it gives you about which past situation it resembles. Triggers are not random - the same kind turning up three times or more is the fingerprint of a limiting belief, and it goes to the monthly review 3.1 (see 5.2).":
        "触发诱因：当下那件具体的、让你觉得熟悉的事 / 场景 / 人，以及让你想起过去哪一次相似的线索。它并不是随机的——同类攒够 3 个，就是某个限制性观念的指纹，该交给月课 3.1（见 5.2）。",
    "Quick log": "快速记录",
    "Log how you feel right now": "记一笔现在的状态",
    "Write down what you feel and what you need to do to move back toward your source.":
        "写下你现在的感受，以及需要做什么才能重新靠向根源。",
    "To complete": "待补全",
    "No state records yet": "还没有状态记录",
    "Every state record, newest first. An entry marked \"to complete\" came from the quick log on the trigger page - open it and fill in the rest.":
        "全部状态记录，新的排前面。标着「待补全」的是从触因页「记一次」留下的半成品——点进去把剩下的格子填上。",
    "Nothing to save yet": "还什么都没填，没什么可保存的",
    "Delete state record": "删除状态记录",
    "Delete this state record?": "确定删除这条状态记录？",

    # —— 统计页（需求 16）——
    "Last 30 days": "近 30 天",
    "Last 90 days": "近 90 天",
    "Last year": "近一年",
    "Rhythm completion": "节拍完成度",
    "Micro failure ranking": "微观失败排行",
    "Trigger ranking": "触因排行",
    "Total": "累计",
    "No data yet": "还没有数据",

    # —— 设置页的节拍组（需求 14）——
    "Review rhythm": "复盘节拍",
    "Weekly review anchor day": "周课锚定日",
    "The weekly review is anchored to this weekday. The book puts it on Friday, so the week's material is still fresh and the weekend is still ahead of you.":
        "周课锚定在每星期的这一天。书里定的是周五：这一周的材料还新鲜，而周末还在前面。",
    "Weekly review grace days": "周课宽容天数",
    "How many days after the anchor the weekly review is still merely pending instead of overdue. The last week of the month is the usual reason to widen this.":
        "锚定日之后还能算「待办」而不是「已逾期」的天数。月末那一周通常就是要放宽它的原因。",
    "Monthly review grace days": "月课宽容天数",
    "How many days into the next month the monthly review is still merely pending. The end of a month is a busy time; a few days of slack keeps it from being marked overdue on day one.":
        "次月还能算「待办」的天数。月末本来就忙，留几天缓冲，免得月课在 1 号就被标成已逾期。",

    # —— 顺手补上的既有缺口（上游就漏了，不是本次改动引入的）——
    "Check frequency": "数频率",
    "Note": "备注",
    "Do not show again": "不再显示",
    "Update setting": "更新设置",
    "Update method": "更新方式",
    "Update prompt": "更新提示",
}


def append(path, values):
    with io.open(path, encoding="utf-8") as fh:
        text = fh.read()

    newline = "\r\n" if "\r\n" in text else "\n"

    # 已有 key 不覆盖
    existing = json.loads(text)
    todo = [(k, v) for k, v in values.items() if k not in existing]
    if not todo:
        print("  nothing to add")
        return 0

    close = text.rstrip().rfind("}")
    head = text[:close].rstrip()
    tail = text[close:]

    if not head.endswith(","):
        head += ","

    lines = ["  " + json.dumps(k, ensure_ascii=False) + ": " + json.dumps(v, ensure_ascii=False)
             for k, v in todo]

    result = head + newline + ("," + newline).join(lines) + newline + tail

    with io.open(path, "w", encoding="utf-8", newline="") as fh:
        fh.write(result)

    print("  added %d" % len(todo))
    return len(todo)


def main():
    total = 0
    total += append(os.path.join(I18N_DIR, "zh-CN.json"), ZH)
    total += append(os.path.join(I18N_DIR, "en-US.json"), {k: k for k in ZH})
    print("total %d" % total)
    return 0


if __name__ == "__main__":
    sys.exit(main())
