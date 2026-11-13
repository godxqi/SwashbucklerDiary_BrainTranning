using SwashbucklerDiary.Shared;

int fail = 0;
void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {name}{(detail is null ? "" : "   <- " + detail)}");
    if (!ok) fail++;
}

string[] titles = ["生活上的", "学习上的", "三项积极的", "微观失败", "精力", "今天过得怎么样"];
string[] questions = ["发生了什么？", "有没有可能是上升的一部分？", "学到了什么？", "下次怎么做？"];

// 真实调用方 BuildFacetBlockAsync 返回的就是这份纯 bullet 文本，受管标记由 Apply 自己加
string facetBlock = string.Join("\n", new[]
{
    "- **今天过得怎么样**: 悲伤",
    "- **精力**: 3/5",
});

var s = new DailyReviewSections
{
    Life = "L", Study = "S",
    ThreePositives = ["p1", "p2", "p3"],
    MicroFailures = [["m1a", "m1b", "m1c", "m1d"], ["m2a", "m2b", "", ""]],
    EnergyNote = "E", MoodNote = "被否定了",
};

string composed = s.Compose(titles, questions);
string raw = ReviewContentBlock.Apply(composed, facetBlock);
string body = ReviewContentBlock.Strip(raw);

Console.WriteLine($"raw.len={raw.Length} body.len={body.Length} body含start={body.Contains(ReviewContentBlock.StartMarker)} body含end={body.Contains(ReviewContentBlock.EndMarker)}");
Console.WriteLine("---- body ----");
Console.WriteLine(body);
Console.WriteLine("--------------");

Check("1.6 标题写进了正文", body.Contains("## 1.6"));
Check("1.6 的原因写进了正文", body.Contains("被否定了"));
Check("受管区块整个被剥掉", !body.Contains("facets"));

var back = DailyReviewSections.Parse(body);
Check("1.1 往返", back.Life == "L", back.Life);
Check("1.2 往返", back.Study == "S", back.Study);
Check("1.3 三条往返", back.ThreePositives.SequenceEqual(new[] { "p1", "p2", "p3" }), string.Join("|", back.ThreePositives));
Check("1.4 两条往返（新 1.6 没打乱 ### 分块）",
    back.MicroFailures.Count == 2 && back.MicroFailures[0][0] == "m1a" && back.MicroFailures[1][1] == "m2b",
    string.Join(" / ", back.MicroFailures.Select(g => string.Join("|", g))));
Check("1.5 往返", back.EnergyNote == "E", back.EnergyNote);
Check("1.6 往返", back.MoodNote == "被否定了", "[" + back.MoodNote + "]");
Check("拼回去与剥掉区块的原文一致", back.Compose(titles, questions) == body);
Check("IsEmpty 看得到 1.6", !new DailyReviewSections { MoodNote = "x" }.IsEmpty);

// 老格式：1.1~1.5，没有 1.6
string oldRaw = ReviewContentBlock.Apply("## 1.1 生活上的\n老日记的生活\n\n## 1.5 精力\n有点累", facetBlock);
var oldBack = DailyReviewSections.Parse(ReviewContentBlock.Strip(oldRaw));
Check("老格式 1.1 读回", oldBack.Life == "老日记的生活", oldBack.Life);
Check("老格式 1.5 读回", oldBack.EnergyNote == "有点累", "[" + oldBack.EnergyNote + "]");
Check("老格式的 1.6 空着（不报错、不串节）", string.IsNullOrEmpty(oldBack.MoodNote), "[" + oldBack.MoodNote + "]");
Check("老格式重拼不产生 1.6 标题", !oldBack.Compose(titles, questions).Contains("## 1.6"));
Check("老格式重拼后 1.5 没吞掉受管区块", !oldBack.Compose(titles, questions).Contains("facets"));

// 只写原因、不选情绪
var onlyReason = DailyReviewSections.Parse("## 1.6 今天过得怎么样\n就是很烦");
Check("只有 1.6 也读得回", onlyReason.MoodNote == "就是很烦", onlyReason.MoodNote);
Check("只有 1.6 时前面的节空着", string.IsNullOrEmpty(onlyReason.Life) && string.IsNullOrEmpty(onlyReason.EnergyNote));

// 手改过的正文：1.6 在前、1.5 在后
var swapped = DailyReviewSections.Parse("## 1.6 今天过得怎么样\n原因\n\n## 1.5 精力\n累");
Check("手改顺序后 1.6 仍读得回", swapped.MoodNote == "原因", swapped.MoodNote);
Check("手改顺序后 1.5 仍读得回", swapped.EnergyNote == "累", swapped.EnergyNote);

// ============================================================
// 周课：2026-10-06 改了章节顺序（2.1 结算 / 2.2 数频率 / 2.3 提炼 /
// 2.4 四表检查 / 2.5 新事物 / 2.6 下周目标），散文只有 2.1 / 2.4 / 2.5 三节。
// ============================================================
string[] wTitles = ["本周三个目标结算", "四个记录表检查", "本周一件新事物"];
string[] wQuestions = ["状态表问题？", "四个List问题？", "三个优点表问题？", "肯定库问题？"];

var w = new WeeklyReviewSections
{
    SettlementReflection = "本周因为这套系统发生的不一样的事",
    Exits = ["出口一", "出口二\n续行并进来", "出口三", "出口四"],
    NewThing = "试了一道没做过的菜",
};

string wBody = w.Compose(wTitles, wQuestions);
Console.WriteLine("---- 周课正文 ----");
Console.WriteLine(wBody);
Console.WriteLine("------------------");

Check("周课 2.1 标题写进正文", wBody.Contains("## 2.1"));
Check("周课 2.4 标题写进正文", wBody.Contains("## 2.4"));
Check("周课 2.5 标题写进正文", wBody.Contains("## 2.5"));
Check("周课没给 2.2/2.3/2.6 写散文标题（那三节是结构化的）",
    !wBody.Contains("## 2.2") && !wBody.Contains("## 2.3") && !wBody.Contains("## 2.6"));

string wStripped = ReviewContentBlock.Strip(ReviewContentBlock.Apply(wBody, "- **摘要**: x"));
var wBack = WeeklyReviewSections.Parse(wStripped);
Check("周课 结算追问 往返", wBack.SettlementReflection == w.SettlementReflection,
    "[" + wBack.SettlementReflection + "]");
Check("周课 四个记录表 往返（含多行续接）", wBack.Exits.SequenceEqual(w.Exits),
    string.Join(" / ", wBack.Exits));
Check("周课 新事物 往返", wBack.NewThing == w.NewThing, "[" + wBack.NewThing + "]");
Check("周课重拼与原文一致", wBack.Compose(wTitles, wQuestions) == wBody);

// 老格式（改序之前的 2.4/2.5/2.6）——只报告行为，不当成失败：
// 编号是锚点，所以老正文会被按新编号重读。主人的库里 0 条周课，所以没有实际影响。
var legacy = WeeklyReviewSections.Parse("## 2.4 老结算\n老的结算追问\n\n## 2.5 老四处出口\n- 老出口1\n\n## 2.6 老新事物\n老的新事物");
Console.WriteLine("NOTE 老格式周课正文按新编号重读 -> 2.4 落到「四个记录表检查」: "
    + (legacy.Exits[0] == "老的结算追问" ? "是（符合预期）" : "否：" + string.Join("|", legacy.Exits)));

Console.WriteLine();
Console.WriteLine(fail == 0 ? "RESULT=ALL OK" : $"RESULT=FAILED {fail}");
return fail == 0 ? 0 : 1;
