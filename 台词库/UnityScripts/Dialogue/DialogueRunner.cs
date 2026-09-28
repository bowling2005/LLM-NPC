using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// MonoBehaviour 驱动器：以协程方式驱动台词序列播放。
    /// 挂载到场景中的 DialogueManager GameObject 上即可使用。
    ///
    /// 使用方式：
    ///   1. 在场景中创建空 GameObject，挂载本脚本
    ///   2. 注入 IDialogueUI 实现（你的 UI 层）
    ///   3. 调用 PlayScene / PlayForcedNode / PlayChoice 等方法驱动剧情
    /// </summary>
    public class DialogueRunner : MonoBehaviour
    {
        /// <summary>
        /// UI 层接口：游戏侧需实现此接口来显示台词和选项。
        /// </summary>
        public interface IDialogueUI
        {
            /// <summary>显示一条台词</summary>
            void ShowLine(DialogueLine line, string displayText);

            /// <summary>显示旁白/文书（不同渲染样式）</summary>
            void ShowNarration(DialogueLine line, string displayText);

            /// <summary>显示决策选项</summary>
            void ShowChoice(ChoiceNode node, List<ChoiceOptionUI> options, Action<string> onPick);

            /// <summary>隐藏所有对话 UI</summary>
            void Hide();

            /// <summary>等待玩家点击继续（台词播完后）</summary>
            IEnumerator WaitForContinue();
        }

        /// <summary>传给 UI 层的选项数据</summary>
        [Serializable]
        public class ChoiceOptionUI
        {
            public string Id;           // a / b / c / d
            public string Label;        // 选项文案（或 summary）
            public string Summary;      // 简述
            public bool Available;      // 是否可选
            public bool Hidden;         // 是否隐藏（true=不显示，false=置灰）
        }

        // ═══════════════════════════════════════════════════════
        //  配置
        // ═══════════════════════════════════════════════════════

        [Header("台词库 JSON 路径（相对于 StreamingAssets）")]
        [SerializeField] private string jsonPath = "dialogue/dialogue_bank.json";

        [Header("台词显示速度（字符/秒）")]
        [SerializeField] private float charsPerSecond = 30f;

        [Header("自动播放间隔（秒）")]
        [SerializeField] private float autoPlayDelay = 0.3f;

        // ═══════════════════════════════════════════════════════
        //  状态
        // ═══════════════════════════════════════════════════════

        private IDialogueUI _ui;
        private bool _isPlaying;
        private bool _skipRequested;

        /// <summary>当前是否正在播放台词序列</summary>
        public bool IsPlaying => _isPlaying;

        // ═══════════════════════════════════════════════════════
        //  生命周期
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 初始化 Runner（由游戏管理层调用）。
        /// </summary>
        public void Init(IDialogueUI ui)
        {
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));

            // 加载台词库
            DialogueLoader.Load(jsonPath);

            // 初始化游戏状态
            GameState.Init();

            Debug.Log("[DialogueRunner] 初始化完成");
        }

        private void OnDestroy()
        {
            DialogueLoader.Unload();
        }

        // ═══════════════════════════════════════════════════════
        //  公开 API
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 进入场景并播放 enter_scene 台词序列。
        /// </summary>
        public void PlayScene(string location)
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[DialogueRunner] 正在播放中，请先等待结束");
                return;
            }

            StartCoroutine(PlaySceneCoroutine(location));
        }

        /// <summary>
        /// 触发强制节点（F1/F2/F3）。
        /// </summary>
        public void PlayForcedNode(string eventId)
        {
            if (_isPlaying) return;
            StartCoroutine(PlayForcedNodeCoroutine(eventId));
        }

        /// <summary>
        /// 显示决策节点（等待玩家选择）。
        /// </summary>
        public void PlayChoice(string nodeId, Action<ChoiceNode, string> onPicked = null)
        {
            if (_isPlaying) return;
            StartCoroutine(PlayChoiceCoroutine(nodeId, onPicked));
        }

        /// <summary>
        /// 查看物证。
        /// </summary>
        public void PlayInspectItem(string itemId)
        {
            if (_isPlaying) return;
            StartCoroutine(PlayInspectCoroutine(itemId));
        }

        /// <summary>
        /// 播放指定台词序列（通用）。
        /// </summary>
        public void PlayLines(List<DialogueLine> lines)
        {
            if (_isPlaying) return;
            StartCoroutine(PlayLinesCoroutine(lines));
        }

        /// <summary>
        /// 跳过当前台词（加速/跳过按钮）。
        /// </summary>
        public void Skip()
        {
            _skipRequested = true;
        }

        // ═══════════════════════════════════════════════════════
        //  场景进入协程
        // ═══════════════════════════════════════════════════════

        private IEnumerator PlaySceneCoroutine(string location)
        {
            _isPlaying = true;

            // EnterScene 会递增访问计数并切换当前地点，必须先调用，
            // 后续 forced_node 的 visitIndex 窗口判断才基于正确的计数。
            var enterLines = DialogueManager.EnterScene(location);

            // 先播强制节点：部分 forced_node 台词的 event/eventId 为 null，
            // 无法通过 PlayForcedNode(eventId) 命中，只能按场景兜底触发。
            // 典型例子：D6 警局经理认罪供词（设置 main.manager_confessed，
            // 正典结局 END_CANON 的前置条件）。
            var forcedLines = DialogueManager.TriggerForcedNodeAtScene(location);
            yield return PlayLinesCoroutine(forcedLines);

            if (enterLines.Count == 0)
            {
                // 没有进场台词 → 尝试兜底
                var fallback = DialogueManager.QueryIdleFallback("narrator", location);
                if (fallback != null)
                    yield return PlaySingleLine(fallback);
                else
                    Debug.Log($"[DialogueRunner] 场景 {location} 无台词可播");

                _isPlaying = false;
                yield break;
            }

            yield return PlayLinesCoroutine(enterLines);
            _isPlaying = false;
        }

        private IEnumerator PlayForcedNodeCoroutine(string eventId)
        {
            _isPlaying = true;

            var lines = DialogueManager.TriggerForcedNode(eventId);

            if (lines.Count == 0)
            {
                Debug.LogWarning($"[DialogueRunner] 强制节点 {eventId} 无匹配台词");
                _isPlaying = false;
                yield break;
            }

            yield return PlayLinesCoroutine(lines);
            _isPlaying = false;
        }

        private IEnumerator PlayInspectCoroutine(string itemId)
        {
            _isPlaying = true;

            var lines = DialogueManager.InspectItem(itemId);

            yield return PlayLinesCoroutine(lines);
            _isPlaying = false;
        }

        // ═══════════════════════════════════════════════════════
        //  台词序列播放
        // ═══════════════════════════════════════════════════════

        private IEnumerator PlayLinesCoroutine(List<DialogueLine> lines)
        {
            foreach (var line in lines)
            {
                yield return PlaySingleLine(line);

                // 检查 next 指向
                if (line.Next != null)
                {
                    switch (line.Next.Type)
                    {
                        case "choice_node":
                            yield return PlayChoiceCoroutineInternal(line.Next.Id, null);
                            break;

                        case "line":
                            var nextLine = DialogueLoader.GetLine(line.Next.Id);
                            if (nextLine != null)
                                yield return PlaySingleLine(nextLine);
                            break;

                        case "ending":
                            var ending = DialogueManager.CheckEnding();
                            if (ending != null)
                                yield return PlayEndingCoroutine(ending);
                            break;

                        case "auto":
                        case "none":
                        default:
                            break;
                    }
                }

                // 台词间短暂间隔
                if (autoPlayDelay > 0)
                    yield return new WaitForSeconds(autoPlayDelay);
            }
        }

        /// <summary>
        /// 播放单条台词（含打字效果）。
        /// </summary>
        private IEnumerator PlaySingleLine(DialogueLine line)
        {
            string displayText = DialogueLoader.GetDisplayText(line);

            // 标记已播放 + 应用效果
            DialogueManager.MarkLinePlayed(line);

            // 根据 type 选择渲染方式
            if (line.Type == "narration" || line.Type == "document" || line.Type == "inner")
            {
                _ui.ShowNarration(line, displayText);
            }
            else
            {
                _ui.ShowLine(line, displayText);
            }

            // 打字效果：等待文本显示完毕
            if (charsPerSecond > 0 && !string.IsNullOrEmpty(line.Text))
            {
                float duration = line.Text.Length / charsPerSecond;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (_skipRequested)
                    {
                        _skipRequested = false;
                        break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // 等待玩家点击继续
            yield return _ui.WaitForContinue();
        }

        // ═══════════════════════════════════════════════════════
        //  决策节点协程
        // ═══════════════════════════════════════════════════════

        private IEnumerator PlayChoiceCoroutine(string nodeId, Action<ChoiceNode, string> onPicked)
        {
            _isPlaying = true;
            yield return PlayChoiceCoroutineInternal(nodeId, onPicked);
            _isPlaying = false;
        }

        private IEnumerator PlayChoiceCoroutineInternal(string nodeId, Action<ChoiceNode, string> onPicked)
        {
            var node = DialogueManager.GetChoiceNode(nodeId);
            if (node == null)
            {
                Debug.LogError($"[DialogueRunner] 决策节点不存在：{nodeId}");
                yield break;
            }

            // 先播放 promptLine（引导台词）
            if (!string.IsNullOrEmpty(node.PromptLine))
            {
                var promptLine = DialogueLoader.GetLine(node.PromptLine);
                if (promptLine != null)
                    yield return PlaySingleLine(promptLine);
            }

            // 死锁检测
            if (DialogueManager.IsNodeDeadlocked(node, out var skipLine))
            {
                Debug.LogError($"[DialogueRunner] 决策节点死锁：{nodeId}，所有选项不可用且无出路");
                _ui.Hide();
                yield break;
            }

            if (skipLine != null)
            {
                yield return PlaySingleLine(skipLine);
                yield break;
            }

            // ── 选择循环：maxPicks 控制可选次数 ────────────────
            // 剧本中"搜证选项（选2处）"这类多选节点，需要在一次进入中
            // 允许玩家连续选择 maxPicks 次，而不是一次就结束。
            int maxPicks = Mathf.Max(1, node.MaxPicks);
            int picksUsed = 0;
            var consumedOptions = new HashSet<string>();   // 已消耗（consumable）的选项
            string lastPicked = null;

            while (picksUsed < maxPicks)
            {
                var optionUIs = BuildOptionUIs(node, consumedOptions);

                // 没有可选项了就结束循环（避免死等玩家点击）
                if (optionUIs.Count == 0)
                {
                    // 允许跳过时走 skipLine 兜底
                    if (node.AllowSkip && !string.IsNullOrEmpty(node.SkipLine))
                    {
                        var sl = DialogueLoader.GetLine(node.SkipLine);
                        if (sl != null) yield return PlaySingleLine(sl);
                    }

                    break;
                }

                string pickedOptionId = null;
                bool choiceMade = false;

                _ui.ShowChoice(node, optionUIs, (optionId) =>
                {
                    pickedOptionId = optionId;
                    choiceMade = true;
                });

                // 允许跳过的节点：给 UI 一个"跳过"信号的机会
                // （UI 层可回调 null 表示玩家选择跳过）
                while (!choiceMade)
                    yield return null;

                if (string.IsNullOrEmpty(pickedOptionId))
                    break;   // 玩家主动跳过

                lastPicked = pickedOptionId;
                picksUsed++;

                // 处理选择：记录 choiceLog + 应用 effects + 取回应台词
                var responseLines = DialogueManager.PickOption(node, pickedOptionId);
                yield return PlayLinesCoroutine(responseLines);

                // 标记已消耗的选项
                var picked = node.Options.Find(o => o.Id == pickedOptionId);
                if (picked != null && picked.Consumable)
                    consumedOptions.Add(pickedOptionId);
            }

            // 播放 onExhausted 收束台词
            if (!string.IsNullOrEmpty(node.OnExhausted))
            {
                var exhaustedLine = DialogueLoader.GetLine(node.OnExhausted);
                if (exhaustedLine != null)
                    yield return PlaySingleLine(exhaustedLine);
            }

            _ui.Hide();
            onPicked?.Invoke(node, lastPicked);
        }

        /// <summary>
        /// 构建当前轮次可选的选项 UI 列表。
        /// 过滤规则：已消耗的不显示；hidden=true 且条件不满足的不显示；
        /// 条件不满足但 hidden=false 的显示为置灰（Available=false）。
        /// </summary>
        private List<ChoiceOptionUI> BuildOptionUIs(ChoiceNode node, HashSet<string> consumedOptions)
        {
            var optionUIs = new List<ChoiceOptionUI>();

            foreach (var opt in node.Options)
            {
                if (consumedOptions.Contains(opt.Id))
                    continue;

                bool available = DialogueManager.IsOptionAvailable(opt);
                bool hidden = opt.Hidden && !available;

                if (hidden)
                    continue;

                optionUIs.Add(new ChoiceOptionUI
                {
                    Id = opt.Id,
                    Label = !string.IsNullOrEmpty(opt.Label) ? opt.Label : opt.Summary,
                    Summary = opt.Summary,
                    Available = available,
                    Hidden = false
                });
            }

            return optionUIs;
        }

        // ═══════════════════════════════════════════════════════
        //  结局协程
        // ═══════════════════════════════════════════════════════

        private IEnumerator PlayEndingCoroutine(Ending ending)
        {
            Debug.Log($"[DialogueRunner] 播放结局：{ending.Name}");

            var lines = new List<DialogueLine>();
            foreach (var lineId in ending.Lines)
            {
                var line = DialogueLoader.GetLine(lineId);
                if (line != null)
                    lines.Add(line);
            }

            yield return PlayLinesCoroutine(lines);

            // 结局播完后通知游戏层
            Debug.Log($"[DialogueRunner] 结局播放完毕：{ending.Name}");
        }
    }
}
