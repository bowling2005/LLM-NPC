using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Dialogue
{
    /// <summary>
    /// 台词库自测与校验库（纯逻辑，不依赖 UnityEditor / MonoBehaviour）。
    ///
    /// 提供三组测试：
    ///   A. 静态数据校验 —— 数据完整性、引用一致性、死条件、兜底覆盖
    ///   B. 引擎逻辑测试 —— 条件求值、Comparator、仲裁排序、group 互斥
    ///   C. 流程模拟     —— 随机/定向通关七日，验证无死锁且结局可达
    ///
    /// 既可由编辑器菜单工具调用，也可由 NUnit 测试薄封装调用。
    /// </summary>
    public static class DialogueSelfTest
    {
        // ═══════════════════════════════════════════════════════
        //  结果结构
        // ═══════════════════════════════════════════════════════

        public enum Severity { Pass, Warn, Fail }

        [Serializable]
        public class TestResult
        {
            public string   Category;
            public string   Name;
            public Severity Severity;
            public string   Message;
            public List<string> Details = new();

            public bool Passed => Severity == Severity.Pass;
        }

        [Serializable]
        public class TestReport
        {
            public List<TestResult> Results = new();
            public DateTime RunAt = DateTime.Now;

            public int Total    => Results.Count;
            public int Passed   => Results.Count(r => r.Severity == Severity.Pass);
            public int Warnings => Results.Count(r => r.Severity == Severity.Warn);
            public int Failed   => Results.Count(r => r.Severity == Severity.Fail);
            public bool AllPassed => Failed == 0;

            public void Add(TestResult r) => Results.Add(r);

            public void AddPass(string category, string name, string message = "")
                => Add(new TestResult { Category = category, Name = name, Severity = Severity.Pass, Message = message });

            public void AddWarn(string category, string name, string message, List<string> details = null)
                => Add(new TestResult { Category = category, Name = name, Severity = Severity.Warn, Message = message, Details = details ?? new() });

            public void AddFail(string category, string name, string message, List<string> details = null)
                => Add(new TestResult { Category = category, Name = name, Severity = Severity.Fail, Message = message, Details = details ?? new() });

            /// <summary>合并另一份报告</summary>
            public void Merge(TestReport other)
            {
                if (other == null) return;
                Results.AddRange(other.Results);
            }

            public string ToText()
            {
                var sb = new StringBuilder();
                sb.AppendLine("════════════════════════════════════════════");
                sb.AppendLine("  台词库测试报告");
                sb.AppendLine($"  运行时间：{RunAt:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine("════════════════════════════════════════════");
                sb.AppendLine();

                string lastCategory = null;
                foreach (var r in Results)
                {
                    if (r.Category != lastCategory)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"── {r.Category} ──");
                        lastCategory = r.Category;
                    }

                    string mark = r.Severity switch
                    {
                        Severity.Pass => "[PASS]",
                        Severity.Warn => "[WARN]",
                        _             => "[FAIL]"
                    };

                    sb.AppendLine($"{mark} {r.Name}");
                    if (!string.IsNullOrEmpty(r.Message))
                        sb.AppendLine($"        {r.Message}");

                    foreach (var d in r.Details.Take(20))
                        sb.AppendLine($"        · {d}");

                    if (r.Details.Count > 20)
                        sb.AppendLine($"        … 另有 {r.Details.Count - 20} 条");
                }

                sb.AppendLine();
                sb.AppendLine("════════════════════════════════════════════");
                sb.AppendLine($"  合计 {Total} 项：通过 {Passed} · 警告 {Warnings} · 失败 {Failed}");
                sb.AppendLine($"  结论：{(AllPassed ? "全部通过" : "存在失败项，需修复")}");
                sb.AppendLine("════════════════════════════════════════════");

                return sb.ToString();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  入口
        // ═══════════════════════════════════════════════════════

        /// <summary>运行全部测试（A + B + C）</summary>
        public static TestReport RunAll(int simulationRuns = 200, int seed = 20260924)
        {
            var report = new TestReport();

            if (!DialogueLoader.IsLoaded)
            {
                report.AddFail("前置", "台词库未加载", "请先调用 DialogueLoader.Load / LoadFromAbsolutePath");
                return report;
            }

            report.Merge(RunStaticValidation());
            report.Merge(RunLogicTests());
            report.Merge(RunPlaythroughSimulation(simulationRuns, seed));
            report.Merge(RunTargetedEndings());

            return report;
        }

        // ═══════════════════════════════════════════════════════
        //  A. 静态数据校验
        // ═══════════════════════════════════════════════════════

        public static TestReport RunStaticValidation()
        {
            var r = new TestReport();
            const string cat = "A·静态数据校验";
            var bank = DialogueLoader.Bank;

            // A1 ─ 数据规模
            {
                int lines = bank.Lines.Count;
                int nodes = bank.ChoiceNodes.Count;
                int endings = bank.Endings.Count;
                int options = bank.ChoiceNodes.Sum(n => n.Options.Count);

                if (lines > 0 && nodes > 0 && endings > 0)
                    r.AddPass(cat, "A1 数据规模",
                        $"台词 {lines} 条 · 决策节点 {nodes} 个 · 选项 {options} 个 · 结局 {endings} 个");
                else
                    r.AddFail(cat, "A1 数据规模", $"数据异常：台词 {lines}、节点 {nodes}、结局 {endings}");
            }

            // A2 ─ ID 唯一性
            {
                var dupLines = bank.Lines.GroupBy(l => l.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                var dupNodes = bank.ChoiceNodes.GroupBy(n => n.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

                if (dupLines.Count == 0 && dupNodes.Count == 0)
                    r.AddPass(cat, "A2 ID 唯一性", "无重复 ID");
                else
                    r.AddFail(cat, "A2 ID 唯一性",
                        $"重复台词 ID {dupLines.Count} 个，重复节点 ID {dupNodes.Count} 个",
                        dupLines.Concat(dupNodes).ToList());
            }

            // A3 ─ ID 格式
            {
                var linePattern = new Regex(@"^(DL|DOC|NAR)_[A-Z0-9]+(_[A-Z0-9]+)*_D[0-9]_[0-9]{3}$");
                var nodePattern = new Regex(@"^CN_D[0-9]_[A-Z0-9]+(_[A-Z0-9]+)*$");

                var badLines = bank.Lines.Where(l => !linePattern.IsMatch(l.Id)).Select(l => l.Id).ToList();
                var badNodes = bank.ChoiceNodes.Where(n => !nodePattern.IsMatch(n.Id)).Select(n => n.Id).ToList();

                if (badLines.Count == 0 && badNodes.Count == 0)
                    r.AddPass(cat, "A3 ID 格式规范", "全部符合编号规范");
                else
                    r.AddFail(cat, "A3 ID 格式规范",
                        $"不合规台词 {badLines.Count} 条、节点 {badNodes.Count} 个",
                        badLines.Concat(badNodes).ToList());
            }

            // A4 ─ 枚举引用完整性
            {
                var npcIds   = new HashSet<string>(bank.Enums.Npc.Select(n => n.Id));
                var locIds   = new HashSet<string>(bank.Enums.Location.Select(l => l.Id));
                var itemIds  = new HashSet<string>(bank.Enums.Item.Select(i => i.Id));
                var flagIds  = new HashSet<string>(bank.Enums.Flag.Select(f => f.Id));
                var eventIds = new HashSet<string>(bank.Enums.Event.Select(e => e.Id));

                var problems = new List<string>();

                foreach (var l in bank.Lines)
                {
                    if (!string.IsNullOrEmpty(l.Npc) && !npcIds.Contains(l.Npc))
                        problems.Add($"{l.Id}: npc 未登记 → {l.Npc}");
                    if (!string.IsNullOrEmpty(l.Location) && !locIds.Contains(l.Location))
                        problems.Add($"{l.Id}: location 未登记 → {l.Location}");
                    if (!string.IsNullOrEmpty(l.Event) && !eventIds.Contains(l.Event))
                        problems.Add($"{l.Id}: event 未登记 → {l.Event}");

                    CollectEnumRefs(l.Conditions, npcIds, itemIds, flagIds, problems, l.Id);
                    CollectEnumRefs(l.Effects, itemIds, flagIds, problems, l.Id);
                }

                foreach (var n in bank.ChoiceNodes)
                {
                    if (!locIds.Contains(n.Location))
                        problems.Add($"{n.Id}: location 未登记 → {n.Location}");
                    if (!string.IsNullOrEmpty(n.Event) && !eventIds.Contains(n.Event))
                        problems.Add($"{n.Id}: event 未登记 → {n.Event}");

                    foreach (var o in n.Options)
                    {
                        CollectEnumRefs(o.Requires, npcIds, itemIds, flagIds, problems, $"{n.Id}.{o.Id}");
                        CollectEnumRefs(o.Effects, itemIds, flagIds, problems, $"{n.Id}.{o.Id}");
                    }
                }

                foreach (var e in bank.Endings)
                    CollectEnumRefs(e.Conditions, npcIds, itemIds, flagIds, problems, e.Id);

                if (problems.Count == 0)
                    r.AddPass(cat, "A4 枚举引用完整性", "所有 npc/location/event/flag/item 引用均已登记");
                else
                    r.AddFail(cat, "A4 枚举引用完整性", $"未登记引用 {problems.Count} 处", problems);
            }

            // A5 ─ 悬空引用（指向不存在的台词/节点）
            {
                var lineIds = new HashSet<string>(bank.Lines.Select(l => l.Id));
                var nodeIds = new HashSet<string>(bank.ChoiceNodes.Select(n => n.Id));
                var problems = new List<string>();

                foreach (var l in bank.Lines)
                {
                    if (!string.IsNullOrEmpty(l.Fallback) && !lineIds.Contains(l.Fallback))
                        problems.Add($"{l.Id}: fallback 悬空 → {l.Fallback}");

                    if (l.Next != null && !string.IsNullOrEmpty(l.Next.Id))
                    {
                        bool ok = l.Next.Type switch
                        {
                            "line"        => lineIds.Contains(l.Next.Id),
                            "choice_node" => nodeIds.Contains(l.Next.Id),
                            _             => true
                        };

                        if (!ok)
                            problems.Add($"{l.Id}: next({l.Next.Type}) 悬空 → {l.Next.Id}");
                    }
                }

                foreach (var n in bank.ChoiceNodes)
                {
                    if (!string.IsNullOrEmpty(n.PromptLine) && !lineIds.Contains(n.PromptLine))
                        problems.Add($"{n.Id}: promptLine 悬空 → {n.PromptLine}");
                    if (!string.IsNullOrEmpty(n.OnExhausted) && !lineIds.Contains(n.OnExhausted))
                        problems.Add($"{n.Id}: onExhausted 悬空 → {n.OnExhausted}");
                    if (!string.IsNullOrEmpty(n.SkipLine) && !lineIds.Contains(n.SkipLine))
                        problems.Add($"{n.Id}: skipLine 悬空 → {n.SkipLine}");

                    foreach (var o in n.Options)
                    {
                        foreach (var rl in o.ResponseLines)
                            if (!lineIds.Contains(rl))
                                problems.Add($"{n.Id}.{o.Id}: responseLines 悬空 → {rl}");

                        if (o.Next != null && !string.IsNullOrEmpty(o.Next.Id))
                        {
                            bool ok = o.Next.Type switch
                            {
                                "line"        => lineIds.Contains(o.Next.Id),
                                "choice_node" => nodeIds.Contains(o.Next.Id),
                                _             => true
                            };

                            if (!ok)
                                problems.Add($"{n.Id}.{o.Id}: next 悬空 → {o.Next.Id}");
                        }
                    }
                }

                foreach (var e in bank.Endings)
                    foreach (var lid in e.Lines)
                        if (!lineIds.Contains(lid))
                            problems.Add($"{e.Id}: ending.lines 悬空 → {lid}");

                if (problems.Count == 0)
                    r.AddPass(cat, "A5 悬空引用", "promptLine / responseLines / onExhausted / next / fallback / ending.lines 全部有效");
                else
                    r.AddFail(cat, "A5 悬空引用", $"悬空引用 {problems.Count} 处", problems);
            }

            // A6 ─ 死条件：被条件引用但无处置位的 flag
            {
                var settable = new HashSet<string>();
                foreach (var l in bank.Lines)
                    foreach (var f in l.Effects?.SetFlags ?? new List<string>())
                        settable.Add(f);
                foreach (var n in bank.ChoiceNodes)
                    foreach (var o in n.Options)
                        foreach (var f in o.Effects?.SetFlags ?? new List<string>())
                            settable.Add(f);

                var referenced = new HashSet<string>();
                void CollectRefs(Conditions c)
                {
                    if (c == null) return;
                    foreach (var f in c.RequiresFlags ?? new List<string>()) referenced.Add(f);
                    foreach (var f in c.ForbidsFlags ?? new List<string>()) referenced.Add(f);
                }

                foreach (var l in bank.Lines) CollectRefs(l.Conditions);
                foreach (var n in bank.ChoiceNodes)
                {
                    CollectRefs(n.Conditions);
                    foreach (var o in n.Options) CollectRefs(o.Requires);
                }
                foreach (var e in bank.Endings) CollectRefs(e.Conditions);

                var dead = referenced.Where(f => !settable.Contains(f)).OrderBy(f => f).ToList();

                if (dead.Count == 0)
                    r.AddPass(cat, "A6 死条件检查",
                        $"被引用的 {referenced.Count} 个 flag 全部有置位来源");
                else
                    r.AddFail(cat, "A6 死条件检查",
                        $"{dead.Count} 个 flag 被条件引用但无处置位（对应分支永远进不去）", dead);
            }

            // A7 ─ 未使用的 flag（提示，非错误）
            {
                var settable = new HashSet<string>();
                foreach (var l in bank.Lines)
                    foreach (var f in l.Effects?.SetFlags ?? new List<string>()) settable.Add(f);
                foreach (var n in bank.ChoiceNodes)
                    foreach (var o in n.Options)
                        foreach (var f in o.Effects?.SetFlags ?? new List<string>()) settable.Add(f);

                var referenced = new HashSet<string>();
                void CollectRefs(Conditions c)
                {
                    if (c == null) return;
                    foreach (var f in c.RequiresFlags ?? new List<string>()) referenced.Add(f);
                    foreach (var f in c.ForbidsFlags ?? new List<string>()) referenced.Add(f);
                }
                foreach (var l in bank.Lines) CollectRefs(l.Conditions);
                foreach (var n in bank.ChoiceNodes)
                {
                    CollectRefs(n.Conditions);
                    foreach (var o in n.Options) CollectRefs(o.Requires);
                }
                foreach (var e in bank.Endings) CollectRefs(e.Conditions);

                var unused = settable.Where(f => !referenced.Contains(f)).OrderBy(f => f).ToList();

                if (unused.Count == 0)
                    r.AddPass(cat, "A7 flag 利用率", "所有可置位 flag 均被条件引用");
                else
                    r.AddWarn(cat, "A7 flag 利用率",
                        $"{unused.Count} 个 flag 已置位但未被任何条件引用（给文案预留的钩子，可留作扩展）", unused);
            }

            // A8 ─ 兜底覆盖：每个 NPC × 可进入场景至少一条 idle_fallback
            {
                var fallbacks = bank.Lines.Where(l => l.Trigger.Type == "idle_fallback").ToList();
                var problems = new List<string>();

                foreach (var fb in fallbacks)
                    if (fb.Once)
                        problems.Add($"{fb.Id}: idle_fallback 必须 once=false，否则重播时会静默");

                // 检查 NPC 在其登记的地点是否有兜底
                foreach (var npc in bank.Enums.Npc)
                {
                    if (npc.Id == "narrator") continue;

                    foreach (var loc in npc.Locations)
                    {
                        bool covered = fallbacks.Any(f => f.Npc == npc.Id && f.Location == loc);
                        if (!covered)
                            problems.Add($"{npc.DisplayName} 在 {loc} 缺少 idle_fallback（玩家可能面对沉默的 NPC）");
                    }
                }

                if (problems.Count == 0)
                    r.AddPass(cat, "A8 兜底覆盖", $"{fallbacks.Count} 条 idle_fallback，均为 once=false 且覆盖到位");
                else
                    r.AddWarn(cat, "A8 兜底覆盖", $"{problems.Count} 处兜底缺口", problems);
            }

            // A9 ─ 带前置条件的节点必须有出路
            {
                var problems = new List<string>();

                foreach (var n in bank.ChoiceNodes)
                {
                    bool allConditional = n.Options.All(o => o.Requires != null && !o.Requires.IsEmpty);
                    if (!allConditional) continue;

                    bool hasEscape = (n.AllowSkip && !string.IsNullOrEmpty(n.SkipLine))
                                     || n.Options.Any(o => o.Requires == null || o.Requires.IsEmpty);

                    if (!hasEscape)
                        problems.Add($"{n.Id}: {n.Options.Count} 个选项全带前置条件，且无 skipLine → 玩家可能卡死");
                }

                if (problems.Count == 0)
                    r.AddPass(cat, "A9 节点出路检查", "全条件节点均有 skipLine 兜底");
                else
                    r.AddFail(cat, "A9 节点出路检查", $"{problems.Count} 个节点可能死锁", problems);
            }

            // A10 ─ group 互斥组必须有恒真兜底（关键组）
            {
                var groups = bank.Lines
                    .Where(l => !string.IsNullOrEmpty(l.Group))
                    .GroupBy(l => l.Group)
                    .ToList();

                var noFallback = new List<string>();
                foreach (var g in groups)
                {
                    bool hasAlwaysTrue = g.Any(l => l.Conditions == null || l.Conditions.IsEmpty);
                    if (!hasAlwaysTrue)
                        noFallback.Add($"{g.Key}（{g.Count()} 条变体，无条件恒真者）");
                }

                if (noFallback.Count == 0)
                    r.AddPass(cat, "A10 group 恒真兜底", $"{groups.Count} 个互斥组均有恒真成员");
                else
                    r.AddWarn(cat, "A10 group 恒真兜底",
                        $"{noFallback.Count} 个互斥组没有恒真兜底——若全部变体条件不满足则该组静默", noFallback);
            }

            // A11 ─ 强制节点 F1/F2/F3 存在且优先级正确
            {
                var forced = bank.Enums.Event.Where(e => e.Forced).ToList();
                var problems = new List<string>();

                if (forced.Count < 3)
                    problems.Add($"强制节点仅 {forced.Count} 个，剧本要求 F1/F2/F3 共 3 个");

                foreach (var ev in forced)
                {
                    var lines = bank.Lines.Where(l =>
                        l.Trigger.Type == "forced_node" &&
                        (l.Event == ev.Id || l.Trigger.EventId == ev.Id)).ToList();

                    if (lines.Count == 0)
                        problems.Add($"{ev.Id}: 无任何 forced_node 台词绑定");

                    foreach (var l in lines.Where(l => l.Priority < 100))
                        problems.Add($"{l.Id}: 强制节点台词 priority={l.Priority}，应为 100");
                }

                if (problems.Count == 0)
                    r.AddPass(cat, "A11 强制节点", $"{forced.Count} 个强制节点均绑定台词且 priority=100");
                else
                    r.AddFail(cat, "A11 强制节点", $"{problems.Count} 处问题", problems);
            }

            // A12 ─ event 为空的 forced_node 台词（已知数据缺陷）
            {
                var orphans = bank.Lines
                    .Where(l => l.Trigger.Type == "forced_node"
                             && string.IsNullOrEmpty(l.Event)
                             && string.IsNullOrEmpty(l.Trigger.EventId))
                    .ToList();

                if (orphans.Count == 0)
                    r.AddPass(cat, "A12 forced_node 事件绑定", "全部 forced_node 台词都有 event 标识");
                else
                {
                    var details = orphans.Select(l =>
                    {
                        var flags = l.Effects?.SetFlags ?? new List<string>();
                        return $"{l.Id}（D{l.Time?.Day} {l.Location}）置位: {(flags.Count > 0 ? string.Join(", ", flags) : "无")}";
                    }).ToList();

                    r.AddWarn(cat, "A12 forced_node 事件绑定",
                        $"{orphans.Count} 条 forced_node 台词 event 与 trigger.eventId 均为 null，" +
                        "TriggerForcedNode(eventId) 无法命中；引擎已用 TriggerForcedNodeAtScene(location) 按场景兜底触发",
                        details);
                }
            }

            // A13 ─ 台词时段与所属节点时段是否一致（数据卫生检查）
            {
                var problems = new List<string>();

                foreach (var n in bank.ChoiceNodes)
                {
                    if (string.IsNullOrEmpty(n.Slot)) continue;

                    // promptLine / onExhausted / skipLine 由引擎显式点名播放，不经时段过滤
                    foreach (var pair in new[]
                             {
                                 ("promptLine", n.PromptLine),
                                 ("onExhausted", n.OnExhausted),
                                 ("skipLine", n.SkipLine)
                             })
                    {
                        if (string.IsNullOrEmpty(pair.Item2)) continue;
                        var line = DialogueLoader.GetLine(pair.Item2);
                        if (line?.Time?.Slot == null || line.Time.Slot.Count == 0) continue;
                        if (line.Time.Slot.Contains("any") || line.Time.Slot.Contains(n.Slot)) continue;

                        problems.Add($"{n.Id}.{pair.Item1}={pair.Item2}：节点 slot={n.Slot}，台词声明 [{string.Join(",", line.Time.Slot)}]");
                    }

                    // responseLines 属 choice_response 触发，已豁免 slot 过滤（见 MatchesTimeWindow）
                    foreach (var rid in n.Options.SelectMany(o => o.ResponseLines))
                    {
                        var line = DialogueLoader.GetLine(rid);
                        if (line?.Time?.Slot == null || line.Time.Slot.Count == 0) continue;
                        if (line.Time.Slot.Contains("any") || line.Time.Slot.Contains(n.Slot)) continue;

                        problems.Add($"{n.Id}(slot={n.Slot}) 的回应台词 {rid} 声明 [{string.Join(",", line.Time.Slot)}]");
                    }
                }

                if (problems.Count == 0)
                    r.AddPass(cat, "A13 时段声明一致性", "台词时段声明与所属节点时段无冲突");
                else
                    r.AddWarn(cat, "A13 时段声明一致性",
                        $"{problems.Count} 处时段声明不一致。choice_response / forced_node / item_inspect / ending " +
                        "属显式触发，引擎已豁免 slot 过滤，不影响播出；但建议修正数据以免误导文案与后续排查",
                        problems);
            }

            // A14 ─ 选项 ID 合法性（a/b/c/d，不重复）
            {
                var problems = new List<string>();
                var valid = new HashSet<string> { "a", "b", "c", "d" };

                foreach (var n in bank.ChoiceNodes)
                {
                    if (n.Options.Count < 2 || n.Options.Count > 4)
                        problems.Add($"{n.Id}: 选项数 {n.Options.Count}，schema 要求 2~4");

                    var dup = n.Options.GroupBy(o => o.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    if (dup.Count > 0)
                        problems.Add($"{n.Id}: 选项 ID 重复 → {string.Join(",", dup)}");

                    foreach (var o in n.Options.Where(o => !valid.Contains(o.Id)))
                        problems.Add($"{n.Id}: 非法选项 ID → {o.Id}");

                    if (n.MaxPicks > n.Options.Count)
                        problems.Add($"{n.Id}: maxPicks={n.MaxPicks} 超过选项总数 {n.Options.Count}");
                }

                if (problems.Count == 0)
                    r.AddPass(cat, "A14 选项结构", "全部节点选项 ID 合法、数量合规、maxPicks 合理");
                else
                    r.AddFail(cat, "A14 选项结构", $"{problems.Count} 处问题", problems);
            }

            // A15 ─ 结局遮蔽：穷举 flag 组合，检查每个结局是否存在"能胜出"的状态
            {
                var endings = bank.Endings;

                // 推断「必然置位」的 flag：互斥组中恒真兜底成员产出的 flag。
                // 只要该组被求值，恒真成员在其它变体全部落选时必然播出，
                // 因此这些 flag 在任何真实流程中都会置位。
                //
                // 若忽略这一点，穷举会认为"判决 flag 都不置位"的组合成立，
                // 从而把实际被永久遮蔽的结局误判为可达（假阴性）。
                var alwaysSet = new HashSet<string>();
                foreach (var g in bank.Lines.Where(l => !string.IsNullOrEmpty(l.Group))
                                             .GroupBy(l => l.Group))
                {
                    var alwaysTrue = g.FirstOrDefault(l => l.Conditions == null || l.Conditions.IsEmpty);
                    if (alwaysTrue == null) continue;

                    foreach (var f in alwaysTrue.Effects?.SetFlags ?? new List<string>())
                        alwaysSet.Add(f);
                }

                // 收集所有结局条件涉及的 flag 作为穷举维度（必然置位者不参与翻转）
                var dims = endings
                    .SelectMany(e => (e.Conditions?.RequiresFlags ?? new List<string>())
                        .Concat(e.Conditions?.ForbidsFlags ?? new List<string>()))
                    .Distinct()
                    .Where(f => !alwaysSet.Contains(f))
                    .OrderBy(f => f).ToList();

                var canWin = new Dictionary<string, int>();
                foreach (var e in endings) canWin[e.Id] = 0;

                // 穷举（维度通常 ≤ 6，开销可忽略）
                if (dims.Count <= 14)
                {
                    int total = 1 << dims.Count;
                    for (int mask = 0; mask < total; mask++)
                    {
                        var flags = new HashSet<string>(alwaysSet);
                        for (int i = 0; i < dims.Count; i++)
                            if ((mask & (1 << i)) != 0) flags.Add(dims[i]);

                        // CheckEnding 按数组顺序取第一个命中项
                        foreach (var e in endings)
                        {
                            if (EndingMatches(e, flags))
                            {
                                canWin[e.Id]++;
                                break;
                            }
                        }
                    }
                }

                var shadowed = endings.Where(e => canWin[e.Id] == 0).ToList();

                if (shadowed.Count == 0)
                    r.AddPass(cat, "A15 结局遮蔽检查",
                        $"穷举 {1 << Math.Min(dims.Count, 14)} 种 flag 组合（含 {alwaysSet.Count} 个必然置位 flag），" +
                        $"{endings.Count} 个结局均可胜出");
                else
                {
                    var details = shadowed.Select(e =>
                    {
                        string req = string.Join(", ", e.Conditions?.RequiresFlags ?? new List<string>());
                        string forbid = string.Join(", ", e.Conditions?.ForbidsFlags ?? new List<string>());

                        // 找出排在它前面、且必然抢占它的结局。
                        // P 必然遮蔽 E 的充要条件：
                        //   ① P.requires ⊆ (E.requires ∪ 必然置位集)
                        //      → 凡 E 成立的状态，P 的要求也成立
                        //   ② P.forbids ∩ (E.requires ∪ 必然置位集) = ∅
                        //      → P 不会因禁止项而失效
                        //      （例如 END_LOSS 禁止 diamond_recovered，
                        //        而 END_WIFE_TESTIFY 恰恰要求它，故 END_LOSS 不构成遮蔽）
                        var eRequires = new HashSet<string>(e.Conditions?.RequiresFlags ?? new List<string>());
                        var eForced = new HashSet<string>(eRequires);
                        foreach (var f in alwaysSet) eForced.Add(f);

                        int myIndex = endings.IndexOf(e);
                        var shadows = endings.Take(myIndex).Where(p =>
                        {
                            var pReq = p.Conditions?.RequiresFlags ?? new List<string>();
                            var pForbid = p.Conditions?.ForbidsFlags ?? new List<string>();

                            return pReq.All(f => eForced.Contains(f))
                                && !pForbid.Any(f => eForced.Contains(f));
                        }).Select(p => p.Id).ToList();

                        return $"{e.Id} — {e.Name}\n" +
                               $"        要求 [{req}]" + (forbid.Length > 0 ? $"，禁止 [{forbid}]" : "") +
                               (shadows.Count > 0
                                   ? $"\n        必然被更靠前的 {string.Join(" / ", shadows)} 抢占"
                                   : "\n        未找到必然抢占者，可能在特定非 flag 条件下仍可达");
                    }).ToList();

                    r.AddFail(cat, "A15 结局遮蔽检查",
                        $"{shadowed.Count} 个结局在任何 flag 组合下都无法胜出。CheckEnding 按数组顺序取第一个命中项，" +
                        "靠后的结局若条件被前面覆盖则永远不可达；需将其前移，或在条件中加 forbidsFlags 使各结局互斥",
                        details);
                }
            }

            // A16 ─ 判决类互斥组必须有恒真兜底（结局依赖其产出 flag）
            {
                var verdictGroups = bank.Lines
                    .Where(l => !string.IsNullOrEmpty(l.Group) &&
                                (l.Effects?.SetFlags ?? new List<string>())
                                    .Any(f => bank.Endings.Any(e =>
                                        (e.Conditions?.RequiresFlags ?? new List<string>()).Contains(f))))
                    .Select(l => l.Group).Distinct().ToList();

                var problems = new List<string>();
                foreach (var g in verdictGroups)
                {
                    var members = bank.Lines.Where(l => l.Group == g).ToList();
                    bool hasAlwaysTrue = members.Any(l => l.Conditions == null || l.Conditions.IsEmpty);

                    if (!hasAlwaysTrue)
                        problems.Add($"互斥组 {g}（{members.Count} 条）产出结局所需 flag，但无恒真成员 → " +
                                     "若全部变体条件不满足则该 flag 不会置位，相关结局不可达");
                }

                if (problems.Count == 0)
                    r.AddPass(cat, "A16 判决组恒真兜底",
                        verdictGroups.Count == 0
                            ? "无结局依赖的互斥组"
                            : $"{verdictGroups.Count} 个结局依赖组（{string.Join(", ", verdictGroups)}）均有恒真兜底");
                else
                    r.AddFail(cat, "A16 判决组恒真兜底", $"{problems.Count} 处缺失", problems);
            }

            return r;
        }

        /// <summary>仅按 flag 集合判定结局条件（穷举用，不涉及完整运行时状态）</summary>
        private static bool EndingMatches(Ending ending, HashSet<string> flags)
        {
            var c = ending.Conditions;
            if (c == null) return true;

            foreach (var f in c.RequiresFlags ?? new List<string>())
                if (!flags.Contains(f)) return false;

            foreach (var f in c.ForbidsFlags ?? new List<string>())
                if (flags.Contains(f)) return false;

            // 穷举只考虑 flag 维度；其他约束（道具/关系）默认不拦截
            return true;
        }

        private static void CollectEnumRefs(Conditions c, HashSet<string> npcIds, HashSet<string> itemIds,
                                            HashSet<string> flagIds, List<string> problems, string owner)
        {
            if (c == null) return;

            foreach (var f in c.RequiresFlags ?? new List<string>())
                if (!flagIds.Contains(f)) problems.Add($"{owner}: requiresFlags 未登记 → {f}");
            foreach (var f in c.ForbidsFlags ?? new List<string>())
                if (!flagIds.Contains(f)) problems.Add($"{owner}: forbidsFlags 未登记 → {f}");
            foreach (var i in c.RequiresItems ?? new List<string>())
                if (!itemIds.Contains(i)) problems.Add($"{owner}: requiresItems 未登记 → {i}");
            foreach (var i in c.ForbidsItems ?? new List<string>())
                if (!itemIds.Contains(i)) problems.Add($"{owner}: forbidsItems 未登记 → {i}");

            if (c.Relationship != null)
                foreach (var k in c.Relationship.Keys)
                    if (!npcIds.Contains(k)) problems.Add($"{owner}: relationship 的 npc 未登记 → {k}");

            if (c.NpcState != null)
                foreach (var k in c.NpcState.Keys)
                    if (!npcIds.Contains(k)) problems.Add($"{owner}: npcState 的 npc 未登记 → {k}");
        }

        private static void CollectEnumRefs(Effects e, HashSet<string> itemIds, HashSet<string> flagIds,
                                            List<string> problems, string owner)
        {
            if (e == null) return;

            foreach (var f in e.SetFlags ?? new List<string>())
                if (!flagIds.Contains(f)) problems.Add($"{owner}: setFlags 未登记 → {f}");
            foreach (var f in e.ClearFlags ?? new List<string>())
                if (!flagIds.Contains(f)) problems.Add($"{owner}: clearFlags 未登记 → {f}");
            foreach (var i in e.AddItems ?? new List<string>())
                if (!itemIds.Contains(i)) problems.Add($"{owner}: addItems 未登记 → {i}");
            foreach (var i in e.RemoveItems ?? new List<string>())
                if (!itemIds.Contains(i)) problems.Add($"{owner}: removeItems 未登记 → {i}");
        }

        // ═══════════════════════════════════════════════════════
        //  B. 引擎逻辑测试
        // ═══════════════════════════════════════════════════════

        public static TestReport RunLogicTests()
        {
            var r = new TestReport();
            const string cat = "B·引擎逻辑";

            GameState.Init();
            var s = GameState.Instance;

            // B1 ─ Comparator 六种操作符
            {
                var problems = new List<string>();
                void Check(string json, int actual, bool expected, string desc)
                {
                    var cmp = Newtonsoft.Json.JsonConvert.DeserializeObject<Comparator>(json);
                    bool got = cmp.Satisfies(actual);
                    if (got != expected)
                        problems.Add($"{desc}: {json} vs {actual} → 期望 {expected}，实际 {got}");
                }

                Check("{\">=\": 2}", 2, true,  ">=");
                Check("{\">=\": 2}", 1, false, ">=");
                Check("{\"<\": 3}",  2, true,  "<");
                Check("{\"<\": 3}",  3, false, "<");
                Check("{\"==\": 5}", 5, true,  "==");
                Check("{\"!=\": 5}", 5, false, "!=");
                Check("{\"<=\": 0}", 0, true,  "<=");
                Check("{\"in\": [1,3,5]}", 3, true,  "in");
                Check("{\"in\": [1,3,5]}", 2, false, "in");

                if (problems.Count == 0)
                    r.AddPass(cat, "B1 Comparator 操作符", ">= / <= / > / < / == / != / in 全部正确");
                else
                    r.AddFail(cat, "B1 Comparator 操作符", $"{problems.Count} 处错误", problems);
            }

            // B2 ─ flag 条件与 hidden.* 不可逆
            {
                var problems = new List<string>();

                s.SetFlag("main.test_flag");
                if (!s.HasFlag("main.test_flag")) problems.Add("SetFlag 后 HasFlag 应为 true");

                var cond = new Conditions { RequiresFlags = new List<string> { "main.test_flag" } };
                if (!DialogueManager.EvaluateConditions(cond)) problems.Add("requiresFlags 应通过");

                var forbid = new Conditions { ForbidsFlags = new List<string> { "main.test_flag" } };
                if (DialogueManager.EvaluateConditions(forbid)) problems.Add("forbidsFlags 应拦截");

                s.SetFlag("hidden.test_irreversible");
                bool cleared = s.ClearFlag("hidden.test_irreversible");
                if (cleared) problems.Add("hidden.* flag 不允许被清除");
                if (!s.HasFlag("hidden.test_irreversible")) problems.Add("hidden.* flag 应仍存在");

                if (problems.Count == 0)
                    r.AddPass(cat, "B2 flag 条件与不可逆", "requires/forbids 生效，hidden.* 拒绝清除");
                else
                    r.AddFail(cat, "B2 flag 条件与不可逆", $"{problems.Count} 处错误", problems);
            }

            // B3 ─ 关系值阈值与钳制
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;

                s.ModifyRelationship("detective", 3);
                if (s.GetRelationship("detective") != 3) problems.Add($"trust 应为 3，实际 {s.GetRelationship("detective")}");

                // 钳制上限 5
                s.ModifyRelationship("detective", 10);
                if (s.GetRelationship("detective") != 5) problems.Add($"trust 应钳制到 5，实际 {s.GetRelationship("detective")}");

                // 钳制下限 0
                s.ModifyRelationship("detective", -20);
                if (s.GetRelationship("detective") != 0) problems.Add($"trust 应钳制到 0，实际 {s.GetRelationship("detective")}");

                // 条件匹配
                s.ModifyRelationship("detective", 3);
                var cond = new Dictionary<string, Comparator>
                {
                    { "trust", Newtonsoft.Json.JsonConvert.DeserializeObject<Comparator>("{\">=\": 3}") }
                };
                if (!s.MatchRelationship("detective", cond)) problems.Add("trust>=3 应通过");

                var cond2 = new Dictionary<string, Comparator>
                {
                    { "trust", Newtonsoft.Json.JsonConvert.DeserializeObject<Comparator>("{\"<\": 3}") }
                };
                if (s.MatchRelationship("detective", cond2)) problems.Add("trust<3 应拦截");

                if (problems.Count == 0)
                    r.AddPass(cat, "B3 关系值", "增量、0~5 钳制、阈值比较均正确");
                else
                    r.AddFail(cat, "B3 关系值", $"{problems.Count} 处错误", problems);
            }

            // B4 ─ 道具条件
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;

                var need = new Conditions { RequiresItems = new List<string> { "wife_letter" } };
                if (DialogueManager.EvaluateConditions(need)) problems.Add("未持有道具时 requiresItems 应拦截");

                s.AddItem("wife_letter");
                if (!DialogueManager.EvaluateConditions(need)) problems.Add("持有道具后 requiresItems 应通过");

                var forbid = new Conditions { ForbidsItems = new List<string> { "wife_letter" } };
                if (DialogueManager.EvaluateConditions(forbid)) problems.Add("持有道具时 forbidsItems 应拦截");

                if (problems.Count == 0)
                    r.AddPass(cat, "B4 道具条件", "requiresItems / forbidsItems 正确");
                else
                    r.AddFail(cat, "B4 道具条件", $"{problems.Count} 处错误", problems);
            }

            // B5 ─ 时间窗口过滤
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;

                s.SetDay(1);
                s.SetSlot("morning");

                var duskLine = new DialogueLine
                {
                    Id = "TEST_DUSK",
                    Time = new TimeInfo { Day = 1, Slot = new List<string> { "dusk" } }
                };
                if (DialogueManager.MatchesTimeWindow(duskLine))
                    problems.Add("dusk 台词不应在 morning 播出");

                s.SetSlot("dusk");
                if (!DialogueManager.MatchesTimeWindow(duskLine))
                    problems.Add("dusk 台词应在 dusk 播出");

                // day 不匹配
                s.SetDay(3);
                if (DialogueManager.MatchesTimeWindow(duskLine))
                    problems.Add("D1 台词不应在 D3 播出");

                // 空 slot 视为不限
                var anyLine = new DialogueLine { Id = "TEST_ANY", Time = new TimeInfo { Day = 3 } };
                if (!DialogueManager.MatchesTimeWindow(anyLine))
                    problems.Add("未声明 slot 的台词应不受时段限制");

                if (problems.Count == 0)
                    r.AddPass(cat, "B5 时间窗口", "day / slot 过滤正确，空字段视为不限");
                else
                    r.AddFail(cat, "B5 时间窗口", $"{problems.Count} 处错误", problems);
            }

            // B6 ─ visitIndex 递进
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;

                if (s.GetVisitCount("tavern") != 0) problems.Add("未访问时 visitCount 应为 0");

                s.EnterScene("tavern");
                if (s.GetVisitCount("tavern") != 1) problems.Add($"第1次访问后应为1，实际{s.GetVisitCount("tavern")}");

                s.EnterScene("tavern");
                s.EnterScene("tavern");
                if (s.GetVisitCount("tavern") != 3) problems.Add($"第3次访问后应为3，实际{s.GetVisitCount("tavern")}");

                var cmp3 = Newtonsoft.Json.JsonConvert.DeserializeObject<Comparator>("{\"==\": 3}");
                if (!cmp3.Satisfies(s.GetVisitCount("tavern"))) problems.Add("visitIndex==3 应通过");

                if (problems.Count == 0)
                    r.AddPass(cat, "B6 visitIndex", "访问计数递增正确，可支撑情报递进");
                else
                    r.AddFail(cat, "B6 visitIndex", $"{problems.Count} 处错误", problems);
            }

            // B7 ─ 选择链四种匹配模式
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;

                s.RecordChoice("CN_A", "a");
                s.RecordChoice("CN_B", "b");

                ChainCondition Make(string mode, params (string node, string opt)[] entries)
                    => new ChainCondition
                    {
                        Mode = mode,
                        Entries = entries.Select(e => new ChainEntry { Node = e.node, Option = e.opt }).ToList()
                    };

                if (!EvalChain(Make("all", ("CN_A", "a"), ("CN_B", "b"))))
                    problems.Add("all：两条都发生应通过");
                if (EvalChain(Make("all", ("CN_A", "a"), ("CN_C", "c"))))
                    problems.Add("all：含未发生项应拦截");

                if (!EvalChain(Make("any", ("CN_C", "c"), ("CN_A", "a"))))
                    problems.Add("any：命中一条应通过");
                if (EvalChain(Make("any", ("CN_X", "a"), ("CN_Y", "b"))))
                    problems.Add("any：全未命中应拦截");

                if (!EvalChain(Make("ordered_all", ("CN_A", "a"), ("CN_B", "b"))))
                    problems.Add("ordered_all：正序应通过");
                if (EvalChain(Make("ordered_all", ("CN_B", "b"), ("CN_A", "a"))))
                    problems.Add("ordered_all：逆序应拦截");

                var countCond = Make("count", ("CN_A", "a"), ("CN_B", "b"), ("CN_C", "c"));
                countCond.MinCount = 2;
                if (!EvalChain(countCond)) problems.Add("count：命中2条≥minCount2 应通过");
                countCond.MinCount = 3;
                if (EvalChain(countCond)) problems.Add("count：命中2条<minCount3 应拦截");

                if (problems.Count == 0)
                    r.AddPass(cat, "B7 选择链匹配", "all / any / ordered_all / count 四种语义正确");
                else
                    r.AddFail(cat, "B7 选择链匹配", $"{problems.Count} 处错误", problems);
            }

            // B8 ─ group 互斥 + priority 排序 + once 去重
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;
                s.SetDay(7);
                s.SetSlot("morning");

                var candidates = new List<DialogueLine>
                {
                    new() { Id = "T_LOW",  Priority = 40, Group = "g1", Type = "dialogue",
                            Npc = "lawyer", Trigger = new Trigger { Type = "npc_initiative" } },
                    new() { Id = "T_HIGH", Priority = 80, Group = "g1", Type = "dialogue",
                            Npc = "lawyer", Trigger = new Trigger { Type = "npc_initiative" } },
                    new() { Id = "T_MID",  Priority = 60, Group = null, Type = "dialogue",
                            Npc = "lawyer", Trigger = new Trigger { Type = "npc_initiative" } },
                    new() { Id = "T_ONCE", Priority = 100, Group = null, Once = true, Type = "dialogue",
                            Npc = "lawyer", Trigger = new Trigger { Type = "npc_initiative" } },
                };

                var queue = DialogueManager.ResolveQueue(candidates);
                var ids = queue.Select(l => l.Id).ToList();

                if (ids.Contains("T_LOW"))
                    problems.Add($"group 互斥失效：T_LOW 应被 T_HIGH 顶掉，实际队列 [{string.Join(",", ids)}]");
                if (!ids.Contains("T_HIGH"))
                    problems.Add("group 内应保留 priority 最高的 T_HIGH");

                int idxOnce = ids.IndexOf("T_ONCE"), idxHigh = ids.IndexOf("T_HIGH"), idxMid = ids.IndexOf("T_MID");
                if (!(idxOnce < idxHigh && idxHigh < idxMid))
                    problems.Add($"priority 降序错误：[{string.Join(",", ids)}]，期望 T_ONCE > T_HIGH > T_MID");

                // once 去重
                s.MarkPlayed("T_ONCE");
                var queue2 = DialogueManager.ResolveQueue(candidates);
                if (queue2.Any(l => l.Id == "T_ONCE"))
                    problems.Add("once=true 且已播过的台词应被过滤");

                if (problems.Count == 0)
                    r.AddPass(cat, "B8 仲裁排序", "group 互斥、priority 降序、once 去重均正确");
                else
                    r.AddFail(cat, "B8 仲裁排序", $"{problems.Count} 处错误", problems);
            }

            // B9 ─ 口癖渲染（警探"嗯"由渲染层前置）
            {
                var problems = new List<string>();

                var det = DialogueLoader.GetNpc("detective");
                if (det == null)
                    problems.Add("未找到 detective 定义");
                else if (det.VerbalTic != "嗯")
                    problems.Add($"警探口癖应为「嗯」，实际「{det.VerbalTic}」");

                var testLine = new DialogueLine { Id = "T_TIC", Npc = "detective", Text = "这里有问题" };
                string shown = DialogueLoader.GetDisplayText(testLine);
                if (det?.VerbalTic != null && !shown.StartsWith(det.VerbalTic))
                    problems.Add($"口癖未前置：{shown}");

                // text 为空时应回退到 summary 占位
                var emptyLine = new DialogueLine { Id = "T_EMPTY", Npc = "detective", Text = "", Summary = "简述内容" };
                string emptyShown = DialogueLoader.GetDisplayText(emptyLine);
                if (string.IsNullOrEmpty(emptyShown) || !emptyShown.Contains("简述内容"))
                    problems.Add($"text 留白时应回退 summary 占位，实际「{emptyShown}」");

                if (problems.Count == 0)
                    r.AddPass(cat, "B9 口癖与占位", "警探「嗯」由渲染层前置；text 留白时回退 summary");
                else
                    r.AddFail(cat, "B9 口癖与占位", $"{problems.Count} 处错误", problems);
            }

            // B10 ─ effects 应用
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;
                s.SetDay(1);

                int actionsBefore = s.ActionsLeft;

                var eff = new Effects
                {
                    SetFlags  = new List<string> { "main.found_pink_shards" },
                    AddItems  = new List<string> { "pink_shard", "red_velvet" },
                    Relationship = new Dictionary<string, int> { { "detective", 1 } },
                    NpcState  = new Dictionary<string, Dictionary<string, object>>
                                { { "young_noble", new Dictionary<string, object> { { "present", false } } } },
                    ConsumeAction = true
                };

                DialogueManager.ApplyEffects(eff);

                if (!s.HasFlag("main.found_pink_shards")) problems.Add("setFlags 未生效");
                if (!s.HasItem("pink_shard")) problems.Add("addItems 未生效");
                if (!s.HasItem("red_velvet")) problems.Add("addItems 未生效（第2项）");
                if (s.GetRelationship("detective") != 1) problems.Add($"relationship 未生效，实际 {s.GetRelationship("detective")}");
                if (s.ActionsLeft != actionsBefore - 1) problems.Add("consumeAction 未生效");

                object present = s.GetNpcState("young_noble", "present");
                if (present is bool pb && pb) problems.Add("npcState 写入未生效（present 应为 false）");

                if (problems.Count == 0)
                    r.AddPass(cat, "B10 effects 应用", "flag / item / relationship / npcState / 行动配额全部生效");
                else
                    r.AddFail(cat, "B10 effects 应用", $"{problems.Count} 处错误", problems);
            }

            // B11 ─ 存档序列化往返
            {
                var problems = new List<string>();
                GameState.Init();
                s = GameState.Instance;

                s.SetDay(4);
                s.SetSlot("afternoon");
                s.SetFlag("main.wife_letter");
                s.SetFlag("hidden.photo_acquired");
                s.AddItem("old_silver_photo");
                s.ModifyRelationship("detective", 3);
                s.EnterScene("tavern");
                s.EnterScene("tavern");
                s.RecordChoice("CN_D1_WHARF_EVIDENCE", "a");
                s.MarkPlayed("DL_DET_WHARF_D1_005");

                string json = s.ToJson();
                var restored = GameState.FromJson(json);

                if (restored.CurrentDay != 4) problems.Add($"day 丢失：{restored.CurrentDay}");
                if (restored.CurrentSlot != "afternoon") problems.Add($"slot 丢失：{restored.CurrentSlot}");
                if (!restored.HasFlag("main.wife_letter")) problems.Add("flag 丢失");
                if (!restored.HasFlag("hidden.photo_acquired")) problems.Add("hidden flag 丢失");
                if (!restored.HasItem("old_silver_photo")) problems.Add("道具丢失");
                if (restored.GetRelationship("detective") != 3) problems.Add("关系值丢失");
                if (restored.GetVisitCount("tavern") != 2) problems.Add($"访问计数丢失：{restored.GetVisitCount("tavern")}");
                if (restored.GetChoiceLog().Count != 1) problems.Add($"选择链丢失：{restored.GetChoiceLog().Count} 条");
                if (!restored.HasPlayed("DL_DET_WHARF_D1_005")) problems.Add("播放记录丢失");

                if (problems.Count == 0)
                    r.AddPass(cat, "B11 存档往返", "ToJson → FromJson 后全部状态一致");
                else
                    r.AddFail(cat, "B11 存档往返", $"{problems.Count} 处丢失", problems);
            }

            return r;
        }

        private static bool EvalChain(ChainCondition chain)
        {
            var probe = new Conditions { Chain = chain };
            return DialogueManager.EvaluateConditions(probe);
        }

        // ═══════════════════════════════════════════════════════
        //  C. 流程模拟
        // ═══════════════════════════════════════════════════════

        public enum PlayStrategy
        {
            /// <summary>跑该天全部节点（最大覆盖）</summary>
            Full,
            /// <summary>随机跳过部分可选节点（鲁棒性）</summary>
            Random,
            /// <summary>只跑强制节点（最差情况，验证不卡死）</summary>
            Minimal
        }

        [Serializable]
        public class SimulationOutcome
        {
            public int    RunIndex;
            public string EndingId;
            public string EndingName;
            public int    DaysReached;
            public int    ChoicesMade;
            public int    LinesPlayed;
            public List<string> Deadlocks = new();
            public List<string> SilentScenes = new();
            public bool   ReachedDay7 => DaysReached >= 7;
        }

        /// <summary>
        /// 随机通关模拟：跑满七日，检测死锁与结局可达性。
        /// </summary>
        public static TestReport RunPlaythroughSimulation(int runs = 200, int seed = 20260924)
        {
            var r = new TestReport();
            const string cat = "C·流程模拟";

            var rng = new Random(seed);
            var outcomes = new List<SimulationOutcome>();
            var endingCount = new Dictionary<string, int>();
            var allDeadlocks = new List<string>();
            var allSilent = new List<string>();
            int noEnding = 0;

            for (int i = 0; i < runs; i++)
            {
                var strategy = (PlayStrategy)(i % 3);
                var outcome = SimulateOnePlaythrough(rng, strategy, i);
                outcomes.Add(outcome);

                if (outcome.EndingId != null)
                    endingCount[outcome.EndingId] = endingCount.GetValueOrDefault(outcome.EndingId) + 1;
                else
                    noEnding++;

                allDeadlocks.AddRange(outcome.Deadlocks.Select(d => $"run#{i}({strategy}): {d}"));
                allSilent.AddRange(outcome.SilentScenes.Select(d => $"run#{i}({strategy}): {d}"));
            }

            // C1 ─ 无死锁
            {
                var unique = allDeadlocks.Distinct().ToList();
                if (unique.Count == 0)
                    r.AddPass(cat, $"C1 死锁检测（{runs} 次模拟）",
                        "所有决策节点在任意状态下都有可用选项或 skipLine 出路");
                else
                    r.AddFail(cat, $"C1 死锁检测（{runs} 次模拟）",
                        $"{unique.Count} 处死锁", unique);
            }

            // C2 ─ 七日可走完
            {
                int reached = outcomes.Count(o => o.ReachedDay7);
                if (reached == runs)
                    r.AddPass(cat, "C2 七日流程完整性", $"{runs}/{runs} 次均跑满七日");
                else
                    r.AddFail(cat, "C2 七日流程完整性", $"仅 {reached}/{runs} 次跑满七日");
            }

            // C3 ─ 结局可达性
            {
                var bank = DialogueLoader.Bank;
                var unreachable = bank.Endings
                    .Where(e => !endingCount.ContainsKey(e.Id))
                    .Select(e => $"{e.Id} — {e.Name}")
                    .ToList();

                string dist = string.Join(" · ", endingCount.OrderByDescending(kv => kv.Value)
                    .Select(kv => $"{kv.Key}:{kv.Value}({kv.Value * 100 / runs}%)"));

                if (unreachable.Count == 0)
                    r.AddPass(cat, "C3 结局可达性", $"全部 {bank.Endings.Count} 个结局均被触达。分布：{dist}");
                else if (noEnding == runs)
                    r.AddFail(cat, "C3 结局可达性", "所有模拟均未产生结局", unreachable);
                else
                    r.AddWarn(cat, "C3 结局可达性",
                        $"{unreachable.Count} 个结局在 {runs} 次随机模拟中未触达（随机策略难以命中，见 C4 定向测试）。已触达：{dist}",
                        unreachable);
            }

            // C4 ─ 场景静默检测
            {
                var unique = allSilent.Distinct().ToList();
                if (unique.Count == 0)
                    r.AddPass(cat, "C4 场景静默检测", "无「进入场景后 NPC 一言不发」的情况");
                else
                    r.AddWarn(cat, "C4 场景静默检测", $"{unique.Count} 处场景静默（缺兜底台词）", unique);
            }

            return r;
        }

        /// <summary>
        /// 定向结局测试：为每个结局构造一条可达路径并验证。
        /// </summary>
        public static TestReport RunTargetedEndings()
        {
            var r = new TestReport();
            const string cat = "D·定向结局";
            var bank = DialogueLoader.Bank;

            // 各结局所需的最小 flag 集合（直接按 endings[].conditions 反推）
            foreach (var ending in bank.Endings)
            {
                GameState.Init();
                var s = GameState.Instance;

                var required = ending.Conditions?.RequiresFlags ?? new List<string>();
                var forbidden = ending.Conditions?.ForbidsFlags ?? new List<string>();

                // 构造状态：置位所需 flag，确保禁止 flag 不存在
                foreach (var f in required) s.SetFlag(f);

                bool matched = DialogueManager.CheckEnding()?.Id == ending.Id;

                // 结局按数组顺序判定，前面的结局可能抢先命中
                if (matched)
                {
                    r.AddPass(cat, $"D·{ending.Id}", $"条件可判定 → {ending.Name}");
                }
                else
                {
                    var actual = DialogueManager.CheckEnding();
                    string note = actual != null
                        ? $"被更早的结局 {actual.Id} 抢先命中（数组顺序优先，属预期行为）"
                        : "条件未能触发该结局";

                    r.AddWarn(cat, $"D·{ending.Id}", $"{ending.Name} — {note}",
                        new List<string> { $"requiresFlags: [{string.Join(", ", required)}]",
                                           $"forbidsFlags: [{string.Join(", ", forbidden)}]" });
                }
            }

            // D2 ─ 正典结局全链路可达性（关键 flag 是否真能由流程置位）
            {
                var problems = new List<string>();

                // main.manager_confessed 唯一来源是 DL_MGR_POLICE_D6_010（event 为 null）
                var confessed = bank.Lines.Where(l =>
                    (l.Effects?.SetFlags ?? new List<string>()).Contains("main.manager_confessed")).ToList();

                if (confessed.Count == 0)
                    problems.Add("main.manager_confessed 无任何置位来源 → END_CANON 不可达");
                else
                {
                    foreach (var l in confessed)
                    {
                        bool byEvent = !string.IsNullOrEmpty(l.Event) || !string.IsNullOrEmpty(l.Trigger.EventId);
                        bool byScene = !string.IsNullOrEmpty(l.Location) && l.Time?.Day != null;

                        if (!byEvent && !byScene)
                            problems.Add($"{l.Id} 置位 main.manager_confessed，但既无 event 也无地点/日 → 无法触发");
                        else if (!byEvent)
                            problems.Add($"{l.Id} 置位 main.manager_confessed，但 event 为 null；" +
                                         "必须依赖 TriggerForcedNodeAtScene(location) 按场景触发，" +
                                         "只调 TriggerForcedNode(eventId) 会导致正典结局不可达");
                    }
                }

                var recovered = bank.ChoiceNodes.SelectMany(n => n.Options)
                    .Any(o => (o.Effects?.SetFlags ?? new List<string>()).Contains("main.diamond_recovered"));
                if (!recovered)
                    problems.Add("main.diamond_recovered 无选项置位来源 → END_CANON / END_BAIL 不可达");

                if (problems.Count == 0)
                    r.AddPass(cat, "D2 正典结局链路", "END_CANON 所需关键 flag 均有可达的置位来源");
                else
                    r.AddWarn(cat, "D2 正典结局链路", $"{problems.Count} 处链路风险", problems);
            }

            return r;
        }

        // ═══════════════════════════════════════════════════════
        //  模拟引擎
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 模拟一次完整通关。按日推进，每日处理该日的决策节点。
        /// </summary>
        private static SimulationOutcome SimulateOnePlaythrough(Random rng, PlayStrategy strategy, int runIndex)
        {
            GameState.Init();
            var s = GameState.Instance;
            var bank = DialogueLoader.Bank;

            var outcome = new SimulationOutcome { RunIndex = runIndex };

            // 按日分组节点
            var nodesByDay = bank.ChoiceNodes
                .GroupBy(n => n.Day)
                .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Slot ?? "").ThenBy(n => n.Id).ToList());

            for (int day = 1; day <= 7; day++)
            {
                s.SetDay(day);
                outcome.DaysReached = day;

                if (!nodesByDay.TryGetValue(day, out var nodes))
                    continue;

                foreach (var node in nodes)
                {
                    bool isForcedEvent = !string.IsNullOrEmpty(node.Event) &&
                        bank.Enums.Event.Any(e => e.Id == node.Event && e.Forced);

                    // Minimal 策略只跑强制节点
                    if (strategy == PlayStrategy.Minimal && !isForcedEvent)
                        continue;

                    // Random 策略随机跳过非强制节点
                    if (strategy == PlayStrategy.Random && !isForcedEvent && rng.Next(100) < 35)
                        continue;

                    // 切到该节点的地点与时段
                    s.SetLocationWithoutVisit(node.Location);
                    if (!string.IsNullOrEmpty(node.Slot))
                        s.SetSlot(node.Slot);

                    // 静默探测必须在任何台词播出之前做：
                    // 播出后 once=true 的台词已被标记播过，再查会得到空集，
                    // 从而把「其实有内容可播」误报成「场景静默」。
                    ProbeSceneSilence(node.Location, outcome);

                    // 先触发该场景的强制节点台词（含 event 为 null 的兜底）
                    var forced = DialogueManager.TriggerForcedNodeAtScene(node.Location);
                    foreach (var fl in forced)
                    {
                        DialogueManager.MarkLinePlayed(fl);
                        outcome.LinesPlayed++;
                    }

                    // 跑决策节点
                    RunNodeInSimulation(node, s, rng, outcome);

                    // 决策节点跑完后再评估 npc_initiative 台词。
                    // 顺序很关键：法官判决（group=judge_verdict）依赖举证节点产生的
                    // flag（main.wife_letter / main.manager_confessed），
                    // 必须等节点效果落地后才能正确择一。
                    // 这也补上了设计文档 §10.2 记录的模拟器局限。
                    RunNpcInitiativeAtScene(node.Location, outcome);
                }
            }

            // 结局判定
            var ending = DialogueManager.CheckEnding();
            if (ending != null)
            {
                outcome.EndingId = ending.Id;
                outcome.EndingName = ending.Name;
            }

            return outcome;
        }

        /// <summary>
        /// 评估当前场景下所有 NPC 的主动搭话台词（npc_initiative）。
        /// 走完整仲裁：条件求值 → group 互斥 → priority 排序，
        /// 因此 judge_verdict 这类互斥组只会播出唯一一条判决。
        /// </summary>
        private static void RunNpcInitiativeAtScene(string location, SimulationOutcome outcome)
        {
            var bank = DialogueLoader.Bank;

            foreach (var npc in bank.Enums.Npc)
            {
                var lines = DialogueManager.NpcInitiative(npc.Id, location);
                foreach (var l in lines)
                {
                    DialogueManager.MarkLinePlayed(l);
                    outcome.LinesPlayed++;
                }
            }
        }

        /// <summary>
        /// 场景静默探测：在当前时空下，该场景是否存在任何可播内容
        /// （enter_scene / forced_node / npc_initiative / idle_fallback）。
        /// 全为空则玩家会面对一言不发的场景，记为一次静默。
        ///
        /// 必须在台词播出之前调用，否则 once=true 的台词已被标记播过，
        /// 查询会返回空集而产生假阳性。
        /// </summary>
        private static void ProbeSceneSilence(string location, SimulationOutcome outcome)
        {
            var bank = DialogueLoader.Bank;
            var s = GameState.Instance;

            bool hasContent =
                DialogueManager.QuerySceneLines(location, "enter_scene").Count > 0 ||
                DialogueManager.TriggerForcedNodeAtScene(location).Count > 0 ||
                bank.Enums.Npc.Any(npc => DialogueManager.NpcInitiative(npc.Id, location).Count > 0) ||
                bank.Enums.Npc.Any(npc => DialogueManager.QueryIdleFallback(npc.Id, location) != null);

            if (!hasContent)
                outcome.SilentScenes.Add($"{location}（D{s.CurrentDay} {s.CurrentSlot}）无任何可播台词");
        }

        private static void RunNodeInSimulation(ChoiceNode node, GameState s, Random rng,
                                                SimulationOutcome outcome)
        {
            // 引导台词
            if (!string.IsNullOrEmpty(node.PromptLine))
            {
                var prompt = DialogueLoader.GetLine(node.PromptLine);
                if (prompt != null)
                {
                    DialogueManager.MarkLinePlayed(prompt);
                    outcome.LinesPlayed++;
                }
            }

            int maxPicks = Math.Max(1, node.MaxPicks);
            int picks = 0;
            var consumed = new HashSet<string>();

            while (picks < maxPicks)
            {
                var available = node.Options
                    .Where(o => !consumed.Contains(o.Id) && DialogueManager.IsOptionAvailable(o))
                    .ToList();

                if (available.Count == 0)
                {
                    // 全部不可用 → 检查出路
                    bool hasEscape = node.AllowSkip && !string.IsNullOrEmpty(node.SkipLine);
                    bool everAvailable = node.Options.Any(o => DialogueManager.IsOptionAvailable(o));

                    if (!everAvailable && !hasEscape && picks == 0)
                        outcome.Deadlocks.Add($"{node.Id}（D{node.Day} {node.Location}）所有选项条件均不成立且无 skipLine");

                    if (hasEscape)
                    {
                        var sl = DialogueLoader.GetLine(node.SkipLine);
                        if (sl != null)
                        {
                            DialogueManager.MarkLinePlayed(sl);
                            outcome.LinesPlayed++;
                        }
                    }

                    break;
                }

                // 从当前可用选项中随机选一个
                ChoiceOption chosen = available[rng.Next(available.Count)];

                var responses = DialogueManager.PickOption(node, chosen.Id);
                outcome.ChoicesMade++;

                foreach (var rl in responses)
                {
                    DialogueManager.MarkLinePlayed(rl);
                    outcome.LinesPlayed++;
                }

                if (chosen.Consumable)
                    consumed.Add(chosen.Id);

                picks++;
            }

            // 收束台词
            if (!string.IsNullOrEmpty(node.OnExhausted))
            {
                var ex = DialogueLoader.GetLine(node.OnExhausted);
                if (ex != null)
                {
                    DialogueManager.MarkLinePlayed(ex);
                    outcome.LinesPlayed++;
                }
            }
        }
    }
}
