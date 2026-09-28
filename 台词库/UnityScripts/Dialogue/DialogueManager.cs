using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// 台词管理器：条件求值 + 优先级仲裁 + 播放调度。
    /// 负责决定"当前场景下播放哪条台词"。
    /// </summary>
    public static class DialogueManager
    {
        // ═══════════════════════════════════════════════════════
        //  场景进入
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 进入场景：收集候选台词 → 条件过滤 → 排序 → 返回播放队列。
        /// </summary>
        /// <param name="location">场景 ID，如 "wharf_12"</param>
        /// <returns>按优先级排序的台词条目列表（空列表表示无内容可播）</returns>
        public static List<DialogueLine> EnterScene(string location)
        {
            var state = GameState.Instance;
            state.EnterScene(location);

            // 收集当前场景 + 当前日 的候选台词
            var candidates = DialogueLoader.GetSceneCandidates(location, state.CurrentDay);

            // 只保留 enter_scene 触发的台词
            var enterLines = candidates
                .Where(line => line.Trigger.Type == "enter_scene")
                .ToList();

            // 条件过滤 + 排序
            var result = EvaluateAndSort(enterLines);
            return result;
        }

        /// <summary>
        /// 触发强制节点（F1/F2/F3）。强制节点条件恒真、优先级最高 (100)。
        /// 同时匹配 line.event 与 line.trigger.eventId 两处标识。
        /// </summary>
        public static List<DialogueLine> TriggerForcedNode(string eventId)
        {
            var forcedLines = DialogueLoader.GetLinesByTrigger("forced_node")
                .Where(line => line.Event == eventId || line.Trigger.EventId == eventId)
                .ToList();

            return EvaluateAndSort(forcedLines);
        }

        /// <summary>
        /// 按场景触发全部强制节点台词。
        ///
        /// 为什么需要这个方法：台词库中有 4 条 forced_node 台词的 event 与
        /// trigger.eventId 均为 null（如 DL_MGR_POLICE_D6_010 经理认罪供词），
        /// 仅靠 TriggerForcedNode(eventId) 无法命中。该方法按「当前地点 + 当前日」
        /// 兜底触发，保证这类台词仍能在正确的时空点播出。
        ///
        /// 典型用法：玩家进入地点后先调用本方法，再调用 QuerySceneLines。
        /// </summary>
        public static List<DialogueLine> TriggerForcedNodeAtScene(string location)
        {
            var state = GameState.Instance;

            var forcedLines = DialogueLoader.GetSceneCandidates(location, state.CurrentDay)
                .Where(line => line.Trigger.Type == "forced_node")
                .ToList();

            return EvaluateAndSort(forcedLines);
        }

        /// <summary>
        /// 查看物证：触发 item_inspect 类型的台词。
        /// </summary>
        public static List<DialogueLine> InspectItem(string itemId)
        {
            var inspectLines = DialogueLoader.GetLinesByTrigger("item_inspect")
                .Where(line => line.Trigger.ItemId == itemId)
                .ToList();

            var result = EvaluateAndSort(inspectLines);
            return result;
        }

        /// <summary>
        /// NPC 主动搭话：触发 npc_initiative 类型的台词。
        /// </summary>
        public static List<DialogueLine> NpcInitiative(string npcId, string location)
        {
            var state = GameState.Instance;
            var candidates = DialogueLoader.GetSceneCandidates(location, state.CurrentDay)
                .Where(line => line.Trigger.Type == "npc_initiative" && line.Npc == npcId)
                .ToList();

            return EvaluateAndSort(candidates);
        }

        /// <summary>
        /// 获取兜底台词：当没有内容可播时播放。
        /// </summary>
        public static DialogueLine GetIdleFallback(string npcId, string location)
        {
            var state = GameState.Instance;
            var candidates = DialogueLoader.GetSceneCandidates(location, state.CurrentDay)
                .Where(line => line.Trigger.Type == "idle_fallback" && line.Npc == npcId)
                .ToList();

            var result = EvaluateAndSort(candidates);
            return result.FirstOrDefault();
        }

        // ═══════════════════════════════════════════════════════
        //  决策节点
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 获取决策节点及其选项状态（哪些可用、哪些置灰/隐藏）。
        /// </summary>
        public static ChoiceNode GetChoiceNode(string nodeId)
            => DialogueLoader.GetNode(nodeId);

        /// <summary>
        /// 检查选项是否满足前置条件。
        /// </summary>
        public static bool IsOptionAvailable(ChoiceOption option)
        {
            if (option.Requires == null)
                return true;

            return EvaluateConditions(option.Requires);
        }

        /// <summary>
        /// 玩家选择选项：记录选择 → 应用效果 → 返回回应台词列表。
        /// </summary>
        /// <param name="node">决策节点</param>
        /// <param name="optionId">选项 ID（a/b/c/d）</param>
        /// <returns>回应台词列表</returns>
        public static List<DialogueLine> PickOption(ChoiceNode node, string optionId)
        {
            var option = node.Options.Find(o => o.Id == optionId);
            if (option == null)
            {
                Debug.LogError($"[DialogueManager] 选项 {optionId} 不存在于节点 {node.Id}");
                return new List<DialogueLine>();
            }

            var state = GameState.Instance;

            // 1. 记录选择到 choiceLog
            state.RecordChoice(node.Id, optionId);

            // 2. 应用选项效果
            if (option.Effects != null)
                ApplyEffects(option.Effects);

            // 3. 获取回应台词
            var responseLines = new List<DialogueLine>();
            foreach (var lineId in option.ResponseLines)
            {
                var line = DialogueLoader.GetLine(lineId);
                if (line != null)
                {
                    responseLines.Add(line);
                    if (line.Once)
                        state.MarkPlayed(lineId);
                }
            }

            return responseLines;
        }

        /// <summary>
        /// 检查决策节点是否所有选项都不可用（死锁检测）。
        /// 如果 allowSkip=true 且有 skipLine，返回 skipLine 作为出路。
        /// </summary>
        public static bool IsNodeDeadlocked(ChoiceNode node, out DialogueLine skipLine)
        {
            skipLine = null;

            bool anyAvailable = node.Options.Any(opt => IsOptionAvailable(opt));
            if (anyAvailable)
                return false;

            // 所有选项都不可用 → 检查是否有 skipLine
            if (node.AllowSkip && !string.IsNullOrEmpty(node.SkipLine))
            {
                skipLine = DialogueLoader.GetLine(node.SkipLine);
                return false; // 有出路，不算死锁
            }

            return true; // 真死锁
        }

        // ═══════════════════════════════════════════════════════
        //  结局检查
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 检查当前状态是否触达某个结局。
        /// 按 endings 数组顺序检查（第一个满足条件的结局）。
        /// </summary>
        public static Ending CheckEnding()
        {
            foreach (var ending in DialogueLoader.Bank.Endings)
            {
                if (EvaluateConditions(ending.Conditions))
                {
                    Debug.Log($"[DialogueManager] 结局触发：{ending.Id} — {ending.Name}");
                    return ending;
                }
            }

            return null;
        }

        // ═══════════════════════════════════════════════════════
        //  效果应用
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 应用台词条目或选项的效果到 GameState。
        /// </summary>
        public static void ApplyEffects(Effects effects)
        {
            if (effects == null)
                return;

            var state = GameState.Instance;

            // Flag 操作
            foreach (var flag in effects.SetFlags)
                state.SetFlag(flag);

            foreach (var flag in effects.ClearFlags)
                state.ClearFlag(flag);

            // 道具操作
            foreach (var item in effects.AddItems)
                state.AddItem(item);

            foreach (var item in effects.RemoveItems)
                state.RemoveItem(item);

            // 关系值操作
            if (effects.Relationship != null)
            {
                foreach (var kvp in effects.Relationship)
                    state.ModifyRelationship(kvp.Key, kvp.Value);
            }

            // NPC 状态操作
            if (effects.NpcState != null)
                state.ApplyNpcStateDelta(effects.NpcState);

            // 时间操作
            if (effects.ConsumeAction)
                state.ConsumeAction();

            if (effects.AdvanceDay)
                state.AdvanceDay();

            // 传送
            if (!string.IsNullOrEmpty(effects.Teleport))
            {
                Debug.Log($"[DialogueManager] 传送到：{effects.Teleport}");
                // 由上层游戏逻辑处理实际传送
            }

            // 触发事件
            if (!string.IsNullOrEmpty(effects.TriggerEvent))
            {
                Debug.Log($"[DialogueManager] 触发事件：{effects.TriggerEvent}");
                // 由上层游戏逻辑处理实际事件
            }
        }

        /// <summary>
        /// 标记台词已播放（once=true 的台词）。
        /// </summary>
        public static void MarkLinePlayed(DialogueLine line)
        {
            if (line.Once)
                GameState.Instance.MarkPlayed(line.Id);

            // 应用台词效果
            if (line.Effects != null)
                ApplyEffects(line.Effects);
        }

        // ═══════════════════════════════════════════════════════
        //  条件求值
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 求值条件组（所有子条件为 AND 关系）。
        /// </summary>
        public static bool EvaluateConditions(Conditions cond)
        {
            if (cond == null)
                return true;

            var state = GameState.Instance;

            // requiresFlags
            foreach (var flag in cond.RequiresFlags)
            {
                if (!state.HasFlag(flag))
                    return false;
            }

            // forbidsFlags
            foreach (var flag in cond.ForbidsFlags)
            {
                if (state.HasFlag(flag))
                    return false;
            }

            // requiresItems
            foreach (var item in cond.RequiresItems)
            {
                if (!state.HasItem(item))
                    return false;
            }

            // forbidsItems
            foreach (var item in cond.ForbidsItems)
            {
                if (state.HasItem(item))
                    return false;
            }

            // npcState（NPC 状态断言）
            if (cond.NpcState != null)
            {
                foreach (var kvp in cond.NpcState)
                {
                    if (!state.MatchNpcState(kvp.Key, kvp.Value))
                        return false;
                }
            }

            // relationship（关系值阈值）
            if (cond.Relationship != null)
            {
                foreach (var kvp in cond.Relationship)
                {
                    if (!state.MatchRelationship(kvp.Key, kvp.Value))
                        return false;
                }
            }

            // visitIndex（场景访问次数）
            if (cond.VisitIndex != null && !cond.VisitIndex.IsEmpty)
            {
                int visitCount = state.GetVisitCount(state.CurrentLocation);
                if (!cond.VisitIndex.Satisfies(visitCount))
                    return false;
            }

            // day（当前日）
            if (cond.Day != null && !cond.Day.IsEmpty)
            {
                if (!cond.Day.Satisfies(state.CurrentDay))
                    return false;
            }

            // slot（当前时段）
            if (cond.Slot != null && cond.Slot.Count > 0)
            {
                if (!cond.Slot.Contains(state.CurrentSlot) && !cond.Slot.Contains("any"))
                    return false;
            }

            // weather（当前天气）
            if (cond.Weather != null && cond.Weather.Count > 0)
            {
                if (!cond.Weather.Contains(state.CurrentWeather))
                    return false;
            }

            // actionBudget（行动配额）
            if (cond.ActionBudget?.Remaining != null && !cond.ActionBudget.Remaining.IsEmpty)
            {
                if (!cond.ActionBudget.Remaining.Satisfies(state.ActionsLeft))
                    return false;
            }

            // chain（选择链条件）
            if (cond.Chain != null)
            {
                if (!EvaluateChainCondition(cond.Chain))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 求值选择链条件。
        /// </summary>
        private static bool EvaluateChainCondition(ChainCondition chain)
        {
            var state = GameState.Instance;
            var log = state.GetChoiceLog();

            switch (chain.Mode)
            {
                case "all":
                    // 所有条目都必须在 log 中出现过（不论顺序）
                    foreach (var entry in chain.Entries)
                    {
                        if (!LogContainsEntry(log, entry))
                            return false;
                    }

                    return true;

                case "any":
                    // 至少一条在 log 中出现过
                    foreach (var entry in chain.Entries)
                    {
                        if (LogContainsEntry(log, entry))
                            return true;
                    }

                    return false;

                case "ordered_all":
                    // 全部出现且顺序一致
                    int lastIndex = -1;
                    foreach (var entry in chain.Entries)
                    {
                        int foundIndex = FindEntryIndex(log, entry, lastIndex + 1);
                        if (foundIndex < 0)
                            return false;

                        lastIndex = foundIndex;
                    }

                    return true;

                case "count":
                    // 命中数 >= minCount
                    int count = 0;
                    foreach (var entry in chain.Entries)
                    {
                        if (LogContainsEntry(log, entry))
                            count++;
                    }

                    return chain.MinCount.HasValue && count >= chain.MinCount.Value;

                default:
                    Debug.LogWarning($"[DialogueManager] 未知的 chain mode：{chain.Mode}");
                    return false;
            }
        }

        /// <summary>检查 log 中是否包含指定条目</summary>
        private static bool LogContainsEntry(IReadOnlyList<GameState.ChoiceLogEntry> log, ChainEntry entry)
        {
            foreach (var logEntry in log)
            {
                if (logEntry.Node != entry.Node)
                    continue;

                if (entry.Option != null && logEntry.Option != entry.Option)
                    continue;

                if (entry.Day.HasValue && logEntry.Day != entry.Day.Value)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>在 log 中从 startIndex 开始查找条目</summary>
        private static int FindEntryIndex(IReadOnlyList<GameState.ChoiceLogEntry> log, ChainEntry entry, int startIndex)
        {
            for (int i = startIndex; i < log.Count; i++)
            {
                var logEntry = log[i];
                if (logEntry.Node != entry.Node)
                    continue;

                if (entry.Option != null && logEntry.Option != entry.Option)
                    continue;

                if (entry.Day.HasValue && logEntry.Day != entry.Day.Value)
                    continue;

                return i;
            }

            return -1;
        }

        /// <summary>
        /// 纯查询：获取指定场景当前可播的 enter_scene 台词队列。
        /// 与 EnterScene 不同，**不递增访问计数、不改变任何状态**，可反复调用。
        /// 单元测试与预览工具应用此方法。
        /// </summary>
        public static List<DialogueLine> QuerySceneLines(string location, string triggerType = "enter_scene")
        {
            var state = GameState.Instance;
            var candidates = DialogueLoader.GetSceneCandidates(location, state.CurrentDay)
                .Where(line => line.Trigger.Type == triggerType)
                .ToList();

            return ResolveQueue(candidates);
        }

        /// <summary>
        /// 纯查询：按 NPC + 地点获取当前可播的兜底台词（不改状态）。
        /// </summary>
        public static DialogueLine QueryIdleFallback(string npcId, string location)
        {
            var state = GameState.Instance;
            var candidates = DialogueLoader.GetSceneCandidates(location, state.CurrentDay)
                .Where(line => line.Trigger.Type == "idle_fallback" && line.Npc == npcId)
                .ToList();

            return ResolveQueue(candidates).FirstOrDefault();
        }

        /// <summary>
        /// 仲裁核心：条件过滤 → once 去重 → group 互斥 → 优先级排序。
        /// 公开以便测试直接验证排序与互斥行为。
        /// </summary>
        public static List<DialogueLine> ResolveQueue(List<DialogueLine> candidates)
            => EvaluateAndSort(candidates);

        /// <summary>
        /// 纯查询：列出决策节点中当前可用 / 置灰 / 隐藏的选项。
        /// </summary>
        public static List<OptionStatus> QueryOptionStatus(ChoiceNode node)
        {
            var result = new List<OptionStatus>();
            if (node == null) return result;

            foreach (var opt in node.Options)
            {
                bool available = IsOptionAvailable(opt);
                bool hidden = opt.Hidden && !available;

                result.Add(new OptionStatus
                {
                    OptionId = opt.Id,
                    Available = available,
                    Hidden = hidden,
                    GreyedOut = !available && !hidden,
                    Summary = opt.Summary
                });
            }

            return result;
        }

        /// <summary>选项可用性快照</summary>
        public class OptionStatus
        {
            public string OptionId;
            public bool   Available;
            public bool   Hidden;
            public bool   GreyedOut;
            public string Summary;

            public override string ToString()
                => $"{OptionId}: {(Available ? "可选" : Hidden ? "隐藏" : "置灰")} — {Summary}";
        }

        // ═══════════════════════════════════════════════════════
        //  仲裁排序
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 条件过滤 + group 去重 + 优先级排序。
        /// </summary>
        private static List<DialogueLine> EvaluateAndSort(List<DialogueLine> candidates)
        {
            var state = GameState.Instance;

            // 0. 时间窗口过滤：台词自身声明的 day / slot / visitIndex 必须与当前时空匹配。
            //    这是 conditions 之外的一层约束——conditions 描述"剧情进度"，
            //    time 描述"这句话只在哪个时空成立"。两者缺一不可。
            var inWindow = candidates.Where(MatchesTimeWindow).ToList();

            // 1. 条件过滤
            var passed = inWindow.Where(EvaluateConditions).ToList();

            // 2. 过滤 once=true 且已播过的
            passed = passed.Where(line => !line.Once || !state.HasPlayed(line.Id)).ToList();

            // 3. group 去重：同组只保留 priority 最高的一条
            var grouped = new Dictionary<string, DialogueLine>();
            foreach (var line in passed)
            {
                if (string.IsNullOrEmpty(line.Group))
                    continue;

                if (!grouped.ContainsKey(line.Group) || line.Priority > grouped[line.Group].Priority)
                    grouped[line.Group] = line;
            }

            // 移除同组中非最高的
            passed = passed.Where(line =>
                string.IsNullOrEmpty(line.Group) || grouped[line.Group].Id == line.Id).ToList();

            // 4. 排序：priority 降序 → index 升序 → id 升序
            passed.Sort((a, b) =>
            {
                int cmp = b.Priority.CompareTo(a.Priority);
                if (cmp != 0) return cmp;

                cmp = (a.Index ?? 0).CompareTo(b.Index ?? 0);
                if (cmp != 0) return cmp;

                return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
            });

            return passed;
        }

        /// <summary>
        /// 判定台词自身的时间窗口是否匹配当前时空。
        ///
        /// 严格程度按触发类型区分，这一点很关键：
        ///
        /// · 场景扫描类（enter_scene / npc_initiative / idle_fallback）
        ///   严格匹配 day + slot + weather + visitIndex。
        ///   这类台词是"玩家刚好在这个时空进到这个场景"才该说的话，
        ///   例如酒馆 D1 dusk 的情报不该在 D2 morning 播出。
        ///
        /// · 显式触发类（forced_node / item_inspect / choice_response / ending）
        ///   只匹配 day，豁免 slot / weather / visitIndex。
        ///   这类台词由剧情节点或玩家操作直接点名调用，播出时机已由调用方保证；
        ///   若再卡 slot，数据中节点与台词的时段声明只要有一处不一致
        ///   （实测有 6 处，如 DL_DET_POLICE_D7_060 置位 main.twelve_letters），
        ///   台词就会永远播不出，直接导致结局不可达。
        ///
        /// 空字段一律视为无约束。
        /// </summary>
        public static bool MatchesTimeWindow(DialogueLine line)
        {
            var time = line?.Time;
            if (time == null) return true;

            var state = GameState.Instance;

            // day：null 或 0 表示不限日。所有触发类型都严格匹配。
            if (time.Day.HasValue && time.Day.Value > 0 && time.Day.Value != state.CurrentDay)
                return false;

            // 显式触发类：到此为止，不再检查 slot / weather / visitIndex
            if (IsExplicitTrigger(line.Trigger?.Type))
                return true;

            // slot：空数组或含 "any" 表示不限时段
            if (time.Slot != null && time.Slot.Count > 0 && !time.Slot.Contains("any"))
            {
                if (!time.Slot.Contains(state.CurrentSlot))
                    return false;
            }

            // weather：null 表示不限天气
            if (!string.IsNullOrEmpty(time.Weather) && time.Weather != state.CurrentWeather)
                return false;

            // visitIndex：null 表示不限访问次数
            if (time.VisitIndex.HasValue && !string.IsNullOrEmpty(line.Location))
            {
                if (state.GetVisitCount(line.Location) != time.VisitIndex.Value)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 是否为"显式触发"类型：由剧情节点、玩家操作或事件直接点名，
        /// 播出时机已由调用方保证，无需再受场景时段约束。
        /// </summary>
        public static bool IsExplicitTrigger(string triggerType)
            => triggerType switch
            {
                "forced_node"     => true,
                "item_inspect"    => true,
                "choice_response" => true,
                "ending"          => true,
                _                 => false   // enter_scene / npc_initiative / idle_fallback 走严格匹配
            };
    }
}
