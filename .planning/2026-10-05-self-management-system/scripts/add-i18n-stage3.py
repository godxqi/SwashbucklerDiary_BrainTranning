# -*- coding: utf-8 -*-
"""把阶段 3 新增的 i18n key 追加进 zh-CN / en-US 语言包。

追加而不是重排：解析再整体序列化会把整个文件重排一遍，diff 全是噪音。
en-US 的值就是 key 本身（项目一贯做法）。
只补 zh-CN / en-US；其余 11 个语言包按既定口径留到阶段 4。
"""
import io
import json
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
I18N_DIR = os.path.join(ROOT, "src", "SwashbucklerDiary.Rcl", "wwwroot", "i18n")

ZH = {
    "A book, a film, a conversation, something you realised on your own. Optional.":
        "可以来自书籍、电影、与朋友的交谈，或自己在自我发现之旅中的感悟。可留空。",
    "Accepting this kind of deep criticism is hard, so this step is not optional: give yourself a reward, make time for something you love that is only for you, and list the friends and family who make you feel good and plan to see them soon. This is the one step in the whole system that digs into sore spots - skip the care and you will not want to do the monthly review again.":
        "接受这种深刻的批评、并以开放的心态探究它们是很困难的。所以这一步是强制的：给自己一个应有的奖励，抽出时间做一些只为自己做的事，列出让你感觉良好的朋友和家人并计划不久后与之碰面。别跳过它——三列审视是全系统唯一一个专门挖痛处的动作，不做补偿，下个月就不想再做月课了。",
    "Achievement growth": "成就增长",
    "Achievement list": "成就列表",
    "Add by hand": "手动补充",
    "Add enemy": "加一行敌人表",
    "Add limiting belief": "加一条限制性观念",
    "Add phrase": "加一条短语",
    "Add someone to the positive list": "把人加进积极列表",
    "Added to the positive list": "已加入积极列表",
    "Affirmation": "自我肯定",
    "Affirmation library": "自我肯定库",
    "Archive": "归档区",
    "Archived to list 7.1": "已归档到 7.1",
    "Back into this week's 2.1": "回流本周 2.1",
    "Blocking behaviour": "阻止我的行为",
    "Carried into next week's goals": "已带进下周目标",
    "Carry into next week's goals": "带进下周目标",
    "Counter evidence": "反面证据",
    "Deep belief": "深层观念",
    "Delete enemy": "删除这一行",
    "Delete limiting belief": "删除这条观念",
    "Edit": "编辑",
    "Enemy": "敌人",
    "Fed back": "已回流",
    "Fed back into this week's 2.1": "已回流到本周 2.1",
    "Feed back into this week's 2.1": "回流到本周 2.1",
    "File it here": "归档到这里",
    "Finish this month's review": "完成本月月课",
    "Go back to 3.1 and fill in the counter-evidence column first.": "先回 3.1 把第三列「反面证据」填上。",
    "Goals that carried over": "跨周还没做的目标",
    "Gratitude list": "感恩列表",
    "How much does this belief actually help your wellbeing?": "这些观念对我的幸福有多大帮助？",
    "It keeps coming back. Send it into this week's 2.1 as a fresh undecided failure.":
        "它反复出现。把它作为一条全新的、未定的微观失败送回本周 2.1。",
    "It will be marked as fed back and a fresh undecided failure will be created in this week's 2.1.":
        "它会标记为已回流，并在本周 2.1 里新建一条未定的微观失败。",
    "Limiting belief": "限制性观念",
    "List up to six limiting beliefs you hold about yourself. \"I am not...\" or \"I will not...\" is a good way to start. Then take them one at a time: what evidence supports it, what evidence contradicts it, and how much does it actually help your wellbeing?":
        "列出你对自己持有的最多六种限制性观念——常常是你对自己或他人重复说的，用来解释你认为是可耻或毫无益处的行为。卡壳时用「我并不是……」「我不会……」开头。然后轮流审视每一条：我相信它的证据是什么？反面证据是什么？它对我的幸福到底有多大帮助？",
    "Look at it with suspicion: is the belief objectively true? This column is what 3.2 turns into affirmations.":
        "以怀疑的眼光看待它：这个陈述客观「真实」吗？用过去的经验充分探究。这一格就是 3.2 提炼自我肯定的原料。",
    "Material for this step": "这一步的素材",
    "Micro failures ready to feed back": "该回流的微小失败",
    "Next step": "下一步",
    "No enemy row yet": "还没有敌人表的行",
    "No limiting belief yet": "还没有限制性观念",
    "Not repeated yet": "还没重复过",
    "Nothing archived here yet": "这一栏还空着",
    "Nothing here yet": "还什么都没有",
    "One concrete action this week": "本周可完成的具体操作",
    "One concrete action you can finish this week. Without it the table is only reflection - this is the line that can be carried into next week's goals.":
        "列出一项可以在本周完成的具体操作。没有它，这张表就只是反思——而这一行可以带进下周的三个目标。",
    "Phrase": "短语",
    "Positive list": "积极列表",
    "Previous step": "上一步",
    "Ready to feed back": "可以回流了",
    "Repeated": "重复",
    "Set in an earlier week and never finished. File it here as a plan for the future.":
        "前面某一周定下、一直没做完。把它归档成一条对未来的新计划。",
    "Something you have planned for a long time and never got to. Fitness, a new job, marriage - that kind of thing.":
        "一个你已经计划了相当一段时间、却一直未能实现的目标。健身、找一份新工作、结婚，这一类。",
    "Stuck? Start with \"I am not...\" or \"I will not...\"": "卡壳了？用「我并不是……」「我不会……」开头试试。",
    "Supporting evidence": "支持性证据",
    "The belief driving that behaviour. The most hidden and most deadly one is feeling you do not deserve the future you want.":
        "驱动这种行为的最深层观念。最隐蔽也最致命的一条是：觉得自己不值得自己所渴望的未来。",
    "The four lists are not a to-do list. They are an archive, and only the fourth exit question in 2.5 writes into them.":
        "四个 List 不是待办，是归档区——只有周课 2.5 那一问（第四个出口问题）才往这里写。",
    "The limiting belief": "限制性观念",
    "These tables exist for one moment: when you are in a bad state, open one and read it.":
        "这一组表的用法只有一个：在状态糟糕的时候翻开其中一张，读它。",
    "Three rows, three columns. First column: a goal you have planned for a long time and never reached. Second column: the behaviour that honestly blocks it - write behaviour, not thoughts. Third column: the deeper belief driving that behaviour. Then give every goal one concrete action you can finish this week.":
        "三列三行。第一列：一个你已经计划了很久却一直没能实现的目标。第二列：诚实地记录阻止你实现目标的行为——写行为，不写想法。第三列：驱动这种行为的深层观念。最后给每个目标配一项本周可完成的具体操作。",
    "Turn it into a phrase": "提炼成短语",
    "Turn the counter-evidence from 3.1 into short phrases and put them in your affirmation library. Three or four is enough - easy to remember and easy to keep up.":
        "把 3.1 第三步产出的「反对性陈述」提炼成短语，写进自我肯定库。3–4 条就够——容易记忆，也容易坚持。",
    "Turn this repeated stumbling block into a limiting belief": "把这类反复出现的绊脚石起成一条限制性观念",
    "Turn this repeated trigger into a limiting belief": "把这个反复出现的触因起成一条限制性观念",
    "What is the evidence against it?": "反面证据是什么？",
    "What is the evidence for it?": "我相信这句话的证据是什么？",
    "What you decided": "你决定做什么",
    "Where it came from": "来自哪",
    "Write behaviour, not thoughts. \"I scroll until 1am every night\", not \"I lack discipline\".":
        "写行为，不是写想法。「我每天晚上刷手机到一点」，而不是「我缺乏自律」。",
    "Write the concrete action first.": "请先写下那项具体操作。",
    "Write them into your diary, put them in your phone or on sticky notes, and repeat them mindfully several times a day, imagining they are true.":
        "把它写进日记、输入手机，或写在便利贴上贴到能看见的地方；每天多次用心重复，想象它们都是真的。",

    # ——— 第二轮：`I18n.T(x switch {...})` 里的字面量，第一版扫描器漏掉了 ———
    "Added by hand": "手动补充",
    "Everything you have ever longed for: roles, abilities, resources - plus the achievements that prove your stamina, skill and determination.":
        "列出你曾经渴望的一切：渴望的角色（包括丈夫/父亲这样的角色）、渴望的能力（勇于发声或表达创意的能力）、渴望的资源（财富资产、你所在领域的成功）；再列出那些不在待办清单里、却仍然能证明你的耐力、能力、技巧和决心的成就。",
    "Fill in at least one row of the enemy table.": "先填上敌人表的至少一行。",
    "From 2.1: rejected this week": "来自 2.1：本周落选",
    "From 2.4: the environment, not me": "来自 2.4：原因是环境，不是自己",
    "From a repeated daily thought": "来自日课里反复出现的念头",
    "From a repeated trigger": "来自反复出现的触因",
    "From a weekly goal that carried over": "来自跨周未完成的周目标",
    "From the monthly review 3.1": "来自月课 3.1",
    "Micro failures": "经历的微小失败",
    "New plans for the future": "对未来的新计划",
    "New understanding of myself": "对自我的新认识",
    "Put at least one phrase into the affirmation library.": "至少往自我肯定库里放一条短语。",
    "Reached from 2.4 (the reason was the environment, not you) and 2.1 (the stumbling block that lost this week's single slot). When the same entry shows up three times, it is not a one-off.":
        "入口是周课 2.4（未达成、原因是环境 / 别人不配合）与 2.1（本周落选的绊脚石）。同一条在这里累计出现 3 次，就说明它不是偶发。",
    "The friends and family who make you feel good (and a plan to see them), plus the things you appreciate about yourself.":
        "列出让你感觉良好的朋友和家人，并计划不久后与之碰面；再列出你欣赏自己的地方（比如：我爱我的独立、创造力、善良和脆弱）。",
    "Thoughts about daily life": "对日常的思考",
    "Thoughts that keep coming back in the daily review but never became goals. Added by hand.":
        "日课里反复出现、但没有升级成目标的念头。靠手动补充。",
    "Where the three-column review in 3.1 ends up, plus the patterns behind repeated triggers.":
        "月课 3.1 三列审视的产出、以及攒够次数的触因背后的模式，都落到这里。",
    "Where the weekly goals that carried over past their week end up.":
        "周课 2.3 定下、跨周还没做完的目标落到这里。",
    "Write at least one limiting belief.": "先写下至少一条限制性观念。",
    "Write down what you are grateful for. Stop and actually feel it, one by one, instead of skimming past in your head.":
        "写下你感恩的东西。停下来，一点一滴地真切感受，而不是在脑海里一晃而过。",
    "Write down your self-compensation - this step cannot be skipped.": "先写下你的自我补偿。这一步不许跳过。",
    "Counter-statements": "反对性陈述",
    "Reflowed from 7.4": "来自 7.4 的回流",
    "Active (shows on today's affirmation bar)": "启用（会出现在复盘主页的肯定条上）",
    "Not active": "未启用",
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
