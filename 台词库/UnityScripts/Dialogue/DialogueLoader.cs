using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// 台词库加载器：从 JSON 加载数据并构建索引，供运行时快速查询。
    /// 用法：游戏启动时调用 DialogueLoader.Load("dialogue/dialogue_bank.json");
    /// </summary>
    public static class DialogueLoader
    {
        private static DialogueBank _bank;
        private static bool _loaded;

        // ── 索引 ──────────────────────────────────────────────
        private static readonly Dictionary<string, DialogueLine> _lineIndex = new();
        private static readonly Dictionary<string, ChoiceNode>   _nodeIndex = new();
        private static readonly Dictionary<string, NpcDef>       _npcIndex = new();
        private static readonly Dictionary<string, LocationDef>  _locationIndex = new();
        private static readonly Dictionary<string, ItemDef>      _itemIndex = new();
        private static readonly Dictionary<string, FlagDef>      _flagIndex = new();

        // 按 trigger 类型分组的台词索引
        private static readonly Dictionary<string, List<DialogueLine>> _triggerIndex = new();

        // 按地点 + 日 分组的台词索引（场景进入时快速查候选）
        private static readonly Dictionary<string, List<DialogueLine>> _sceneIndex = new();

        public static DialogueBank Bank
        {
            get
            {
                if (!_loaded)
                    throw new InvalidOperationException("DialogueLoader 尚未加载，请先调用 Load()");
                return _bank;
            }
        }

        public static bool IsLoaded => _loaded;

        // ═══════════════════════════════════════════════════════
        //  加载
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// 从 StreamingAssets 加载台词库 JSON。
        /// path 相对于 Application.streamingAssetsPath，例如 "dialogue/dialogue_bank.json"
        /// </summary>
        public static void Load(string path)
        {
            string fullPath = Path.Combine(Application.streamingAssetsPath, path);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"[DialogueLoader] 台词库文件不存在：{fullPath}");
                return;
            }

            string json = File.ReadAllText(fullPath);
            LoadFromJson(json);
            Debug.Log($"[DialogueLoader] 加载成功：{_bank.Lines.Count} 条台词，{_bank.ChoiceNodes.Count} 个决策节点，{_bank.Endings.Count} 个结局");
        }

        /// <summary>
        /// 从绝对路径加载（EditMode 单元测试用，不依赖 StreamingAssets）。
        /// </summary>
        public static void LoadFromAbsolutePath(string absolutePath)
        {
            if (!File.Exists(absolutePath))
                throw new FileNotFoundException($"[DialogueLoader] 台词库文件不存在：{absolutePath}", absolutePath);

            string json = File.ReadAllText(absolutePath);
            LoadFromJson(json);
            Debug.Log($"[DialogueLoader] 加载成功（绝对路径）：{_bank.Lines.Count} 条台词，{_bank.ChoiceNodes.Count} 个决策节点，{_bank.Endings.Count} 个结局");
        }

        /// <summary>
        /// 从 JSON 字符串加载（支持从 Resources、网络、测试等来源）
        /// </summary>
        public static void LoadFromJson(string json)
        {
            _bank = JsonConvert.DeserializeObject<DialogueBank>(json)
                    ?? throw new InvalidOperationException("[DialogueLoader] JSON 反序列化结果为 null");

            BuildIndexes();
            _loaded = true;
        }

        /// <summary>
        /// 将当前数据序列化回 JSON（用于编辑器保存）
        /// </summary>
        public static string SaveToJson()
        {
            if (!_loaded)
                throw new InvalidOperationException("DialogueLoader 尚未加载");

            return JsonConvert.SerializeObject(_bank, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            });
        }

        /// <summary>
        /// 保存到指定路径
        /// </summary>
        public static void SaveToFile(string path)
        {
            string json = SaveToJson();
            File.WriteAllText(path, json);
            Debug.Log($"[DialogueLoader] 已保存至：{path}");
        }

        // ═══════════════════════════════════════════════════════
        //  索引构建
        // ═══════════════════════════════════════════════════════

        private static void BuildIndexes()
        {
            _lineIndex.Clear();
            _nodeIndex.Clear();
            _npcIndex.Clear();
            _locationIndex.Clear();
            _itemIndex.Clear();
            _flagIndex.Clear();
            _triggerIndex.Clear();
            _sceneIndex.Clear();

            // 台词条目索引
            foreach (var line in _bank.Lines)
            {
                _lineIndex[line.Id] = line;

                // 按 trigger.type 分组
                if (!_triggerIndex.ContainsKey(line.Trigger.Type))
                    _triggerIndex[line.Trigger.Type] = new List<DialogueLine>();
                _triggerIndex[line.Trigger.Type].Add(line);

                // 按 "地点_D日" 分组
                string sceneKey = MakeSceneKey(line);
                if (!string.IsNullOrEmpty(sceneKey))
                {
                    if (!_sceneIndex.ContainsKey(sceneKey))
                        _sceneIndex[sceneKey] = new List<DialogueLine>();
                    _sceneIndex[sceneKey].Add(line);
                }
            }

            // 决策节点索引
            foreach (var node in _bank.ChoiceNodes)
                _nodeIndex[node.Id] = node;

            // 枚举索引
            foreach (var npc in _bank.Enums.Npc)
                _npcIndex[npc.Id] = npc;

            foreach (var loc in _bank.Enums.Location)
                _locationIndex[loc.Id] = loc;

            foreach (var item in _bank.Enums.Item)
                _itemIndex[item.Id] = item;

            foreach (var flag in _bank.Enums.Flag)
                _flagIndex[flag.Id] = flag;
        }

        private static string MakeSceneKey(DialogueLine line)
        {
            if (string.IsNullOrEmpty(line.Location))
                return null;

            int? day = line.Time?.Day;
            return day.HasValue
                ? $"{line.Location}_D{day.Value}"
                : line.Location;
        }

        // ═══════════════════════════════════════════════════════
        //  查询 API
        // ═══════════════════════════════════════════════════════

        /// <summary>按 ID 获取台词条目</summary>
        public static DialogueLine GetLine(string id)
            => _lineIndex.TryGetValue(id, out var line) ? line : null;

        /// <summary>按 ID 获取决策节点</summary>
        public static ChoiceNode GetNode(string id)
            => _nodeIndex.TryGetValue(id, out var node) ? node : null;

        /// <summary>按 ID 获取 NPC 定义</summary>
        public static NpcDef GetNpc(string id)
            => _npcIndex.TryGetValue(id, out var npc) ? npc : null;

        /// <summary>按 ID 获取地点定义</summary>
        public static LocationDef GetLocation(string id)
            => _locationIndex.TryGetValue(id, out var loc) ? loc : null;

        /// <summary>按 ID 获取道具定义</summary>
        public static ItemDef GetItem(string id)
            => _itemIndex.TryGetValue(id, out var item) ? item : null;

        /// <summary>按 ID 获取 flag 定义</summary>
        public static FlagDef GetFlag(string id)
            => _flagIndex.TryGetValue(id, out var flag) ? flag : null;

        /// <summary>获取某 trigger 类型的所有台词</summary>
        public static IReadOnlyList<DialogueLine> GetLinesByTrigger(string triggerType)
            => _triggerIndex.TryGetValue(triggerType, out var list) ? list : Array.Empty<DialogueLine>();

        /// <summary>
        /// 获取当前场景（地点 + 日）的候选台词。
        /// 同时返回不限日的兜底台词。
        /// </summary>
        public static List<DialogueLine> GetSceneCandidates(string location, int day)
        {
            var result = new List<DialogueLine>();

            // 精确匹配 "地点_D日"
            string key = $"{location}_D{day}";
            if (_sceneIndex.TryGetValue(key, out var list))
                result.AddRange(list);

            // 不限日的台词（地点匹配但 day=null）
            if (_sceneIndex.TryGetValue(location, out var fallbackList))
                result.AddRange(fallbackList);

            return result;
        }

        /// <summary>获取所有 NPC 定义列表</summary>
        public static IReadOnlyList<NpcDef> GetAllNpcs()
            => _bank.Enums.Npc;

        /// <summary>获取所有地点定义列表</summary>
        public static IReadOnlyList<LocationDef> GetAllLocations()
            => _bank.Enums.Location;

        /// <summary>获取所有 flag 定义列表</summary>
        public static IReadOnlyList<FlagDef> GetAllFlags()
            => _bank.Enums.Flag;

        /// <summary>获取所有道具定义列表</summary>
        public static IReadOnlyList<ItemDef> GetAllItems()
            => _bank.Enums.Item;

        /// <summary>获取所有决策节点</summary>
        public static IReadOnlyList<ChoiceNode> GetAllNodes()
            => _bank.ChoiceNodes;

        /// <summary>获取所有台词条目</summary>
        public static IReadOnlyList<DialogueLine> GetAllLines()
            => _bank.Lines;

        /// <summary>
        /// 直接访问 bank 对象（供编辑器工具使用）
        /// </summary>
        internal static DialogueBank BankRaw => _bank;

        /// <summary>
        /// 获取台词的显示文本（含口癖前缀处理）
        /// </summary>
        public static string GetDisplayText(DialogueLine line)
        {
            if (string.IsNullOrEmpty(line.Text))
                return string.IsNullOrEmpty(line.Summary) ? "[未填写]" : $"[{line.Summary}]";

            var npc = GetNpc(line.Npc);
            if (npc?.VerbalTic != null)
                return npc.VerbalTic + "，" + line.Text;

            return line.Text;
        }

        /// <summary>
        /// 卸载数据，释放索引
        /// </summary>
        public static void Unload()
        {
            _bank = null;
            _loaded = false;
            _lineIndex.Clear();
            _nodeIndex.Clear();
            _npcIndex.Clear();
            _locationIndex.Clear();
            _itemIndex.Clear();
            _flagIndex.Clear();
            _triggerIndex.Clear();
            _sceneIndex.Clear();
        }
    }
}
