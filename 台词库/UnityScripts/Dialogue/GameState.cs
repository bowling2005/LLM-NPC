using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// 游戏运行时状态管理。
    /// 双层状态：原始选择链 ChoiceLog（只追加）+ 派生状态 Flags/Items/NpcState/Relationship。
    /// </summary>
    [Serializable]
    public class GameState
    {
        // ═══════════════════════════════════════════════════════
        //  静态单例（运行时）
        // ═══════════════════════════════════════════════════════

        private static GameState _instance;

        public static GameState Instance
        {
            get
            {
                if (_instance == null)
                    throw new InvalidOperationException("GameState 尚未初始化，请先调用 Init()");
                return _instance;
            }
        }

        /// <summary>是否存在已初始化的实例（校验/模拟工具用）</summary>
        public static bool HasInstance => _instance != null;

        /// <summary>当前剧本默认天气（按粗略剧本七日天气）</summary>
        public static readonly Dictionary<int, string> DefaultWeatherByDay = new()
        {
            { 1, "cloudy" },     // 阴
            { 2, "cloudy" },     // 多云
            { 3, "clear" },      // 晴
            { 4, "rainy" },      // 阴雨
            { 5, "clear" },      // 晴
            { 6, "clear" },      // 晴
            { 7, "clear" }       // 晴
        };

        // ═══════════════════════════════════════════════════════
        //  系统状态
        // ═══════════════════════════════════════════════════════

        [JsonProperty("day")]              private int _currentDay = 1;
        [JsonProperty("slot")]             private string _currentSlot = "morning";
        [JsonProperty("actionsLeft")]      private int _actionsLeft = 3;
        [JsonProperty("weather")]          private string _currentWeather = "cloudy";
        [JsonProperty("location")]         private string _currentLocation = "";

        public int CurrentDay => _currentDay;
        public string CurrentSlot => _currentSlot;
        public int ActionsLeft => _actionsLeft;
        public string CurrentWeather => _currentWeather;
        public string CurrentLocation => _currentLocation;

        // ═══════════════════════════════════════════════════════
        //  派生状态（第 2 层）
        // ═══════════════════════════════════════════════════════

        [JsonProperty("flags")]        private HashSet<string> _flags = new();
        [JsonProperty("inventory")]    private HashSet<string> _inventory = new();
        [JsonProperty("npcState")]     private Dictionary<string, Dictionary<string, object>> _npcState = new();
        [JsonProperty("relationship")] private Dictionary<string, Dictionary<string, int>> _relationship = new();
        [JsonProperty("visitCount")]   private Dictionary<string, int> _visitCount = new();
        [JsonProperty("playedLines")]  private HashSet<string> _playedLines = new();

        // ═══════════════════════════════════════════════════════
        //  原始选择链（第 1 层）— 只追加不修改
        // ═══════════════════════════════════════════════════════

        [JsonProperty("choiceLog")] private List<ChoiceLogEntry> _choiceLog = new();

        [Serializable]
        public class ChoiceLogEntry
        {
            [JsonProperty("day")]   public int Day;
            [JsonProperty("node")]  public string Node;
            [JsonProperty("option")] public string Option;
            [JsonProperty("ts")]    public long Timestamp;

            public ChoiceLogEntry() { }

            public ChoiceLogEntry(int day, string node, string option)
            {
                Day = day;
                Node = node;
                Option = option;
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        // ═══════════════════════════════════════════════════════
        //  初始化
        // ═══════════════════════════════════════════════════════

        /// <summary>初始化游戏状态（新游戏）。需先完成 DialogueLoader.Load。</summary>
        public static GameState Init()
        {
            var bank = DialogueLoader.Bank;

            _instance = new GameState();
            _instance._currentDay = 1;
            _instance._currentSlot = "morning";
            _instance._actionsLeft = bank.Meta?.Setting?.ActionsPerDay ?? 3;
            _instance._currentWeather = DefaultWeatherByDay.GetValueOrDefault(1, "clear");

            foreach (var npc in bank.Enums.Npc)
            {
                _instance._relationship[npc.Id] = new Dictionary<string, int>
                {
                    { "trust", 0 },
                    { "hostility", 0 }
                };

                _instance._npcState[npc.Id] = new Dictionary<string, object>
                {
                    { "present", true }
                };
            }

            return _instance;
        }

        /// <summary>注入外部实例（模拟测试用，避免污染运行时单例）</summary>
        internal static void UseInstance(GameState state) => _instance = state;

        // ═══════════════════════════════════════════════════════
        //  存档
        // ═══════════════════════════════════════════════════════

        public string ToJson() =>
            JsonConvert.SerializeObject(this, Formatting.Indented);

        public static GameState FromJson(string json)
        {
            var state = JsonConvert.DeserializeObject<GameState>(json);
            _instance = state;
            return state;
        }

        // ═══════════════════════════════════════════════════════
        //  Flag 操作
        // ═══════════════════════════════════════════════════════

        public bool HasFlag(string flagId) => _flags.Contains(flagId);

        /// <summary>设置 flag</summary>
        public void SetFlag(string flagId)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            _flags.Add(flagId);
        }

        /// <summary>清除 flag（hidden.* 与 irreversible 的 flag 禁止清除）</summary>
        public bool ClearFlag(string flagId)
        {
            if (flagId != null && flagId.StartsWith("hidden."))
            {
                Debug.LogWarning($"[GameState] 禁止清除 hidden.* flag：{flagId}");
                return false;
            }

            var def = DialogueLoader.IsLoaded ? DialogueLoader.GetFlag(flagId) : null;
            if (def != null && def.Irreversible)
            {
                Debug.LogWarning($"[GameState] flag 标记为不可逆，禁止清除：{flagId}");
                return false;
            }

            return _flags.Remove(flagId);
        }

        public IReadOnlyCollection<string> GetAllFlags() => _flags;

        // ═══════════════════════════════════════════════════════
        //  道具操作
        // ═══════════════════════════════════════════════════════

        public bool HasItem(string itemId) => _inventory.Contains(itemId);

        public void AddItem(string itemId)
        {
            if (!string.IsNullOrEmpty(itemId)) _inventory.Add(itemId);
        }

        public bool RemoveItem(string itemId) => _inventory.Remove(itemId);

        public IReadOnlyCollection<string> GetInventory() => _inventory;

        // ═══════════════════════════════════════════════════════
        //  关系值
        // ═══════════════════════════════════════════════════════

        /// <summary>获取 NPC 关系值（维度：trust / hostility），未初始化时返回 0</summary>
        public int GetRelationship(string npcId, string dimension = "trust")
        {
            if (_relationship.TryGetValue(npcId, out var dims) && dims.TryGetValue(dimension, out var val))
                return val;
            return 0;
        }

        /// <summary>修改 NPC 关系值（增量），钳制在 0..5</summary>
        public void ModifyRelationship(string npcId, int delta, string dimension = "trust")
        {
            if (!_relationship.ContainsKey(npcId))
                _relationship[npcId] = new Dictionary<string, int>();
            if (!_relationship[npcId].ContainsKey(dimension))
                _relationship[npcId][dimension] = 0;

            _relationship[npcId][dimension] =
                Math.Max(0, Math.Min(5, _relationship[npcId][dimension] + delta));
        }

        /// <summary>
        /// 判定关系值条件：condition 形如 { "trust": {">=": 3} }（即 JSON relationship[npcId]）。
        /// </summary>
        public bool MatchRelationship(string npcId, Dictionary<string, Comparator> condition)
        {
            if (condition == null) return true;

            foreach (var kvp in condition)
            {
                int actual = GetRelationship(npcId, kvp.Key);
                if (kvp.Value == null || kvp.Value.IsEmpty) continue;
                if (!kvp.Value.Satisfies(actual)) return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════
        //  NPC 状态
        // ═══════════════════════════════════════════════════════

        public object GetNpcState(string npcId, string key)
        {
            if (_npcState.TryGetValue(npcId, out var state) && state.TryGetValue(key, out var val))
                return JsonValues.Normalize(val);
            return null;
        }

        public void SetNpcState(string npcId, string key, object value)
        {
            if (!_npcState.ContainsKey(npcId))
                _npcState[npcId] = new Dictionary<string, object>();
            _npcState[npcId][key] = JsonValues.Normalize(value);
        }

        /// <summary>
        /// 应用 effects.npcState 写入：{ npcId: { key: value } }
        /// </summary>
        public void ApplyNpcStateDelta(Dictionary<string, Dictionary<string, object>> delta)
        {
            if (delta == null) return;

            foreach (var npcKvp in delta)
            {
                foreach (var kv in npcKvp.Value)
                    SetNpcState(npcKvp.Key, kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// 判定 npcState 条件：assertion 形如 { "present": false }。
        /// 状态缺失时按"未断言即通过"处理：present 键缺失视为 true（NPC 默认在场），
        /// 其余键缺失视为不满足。
        /// </summary>
        public bool MatchNpcState(string npcId, Dictionary<string, object> assertion)
        {
            if (assertion == null) return true;

            foreach (var kvp in assertion)
            {
                object actual = GetNpcState(npcId, kvp.Key);
                if (actual == null)
                {
                    // 无记录时：present 默认 true，其他键默认 false
                    actual = kvp.Key == "present";
                }

                if (!JsonValues.ValueEquals(actual, kvp.Value)) return false;
            }

            return true;
        }

        // ═══════════════════════════════════════════════════════
        //  场景访问
        // ═══════════════════════════════════════════════════════

        public int GetVisitCount(string locationId)
            => _visitCount.TryGetValue(locationId, out var count) ? count : 0;

        /// <summary>进入场景：递增访问计数并切换当前地点</summary>
        public void EnterScene(string locationId)
        {
            if (!_visitCount.ContainsKey(locationId))
                _visitCount[locationId] = 0;

            _visitCount[locationId]++;
            _currentLocation = locationId;
        }

        /// <summary>仅切换当前地点，不计数（用于同场景内二次评估）</summary>
        public void SetLocationWithoutVisit(string locationId)
            => _currentLocation = locationId;

        // ═══════════════════════════════════════════════════════
        //  台词播放记录
        // ═══════════════════════════════════════════════════════

        public bool HasPlayed(string lineId) => _playedLines.Contains(lineId);

        public void MarkPlayed(string lineId)
        {
            if (!string.IsNullOrEmpty(lineId)) _playedLines.Add(lineId);
        }

        // ═══════════════════════════════════════════════════════
        //  选择链
        // ═══════════════════════════════════════════════════════

        public void RecordChoice(string nodeId, string optionId)
            => _choiceLog.Add(new ChoiceLogEntry(_currentDay, nodeId, optionId));

        public IReadOnlyList<ChoiceLogEntry> GetChoiceLog() => _choiceLog;

        public bool HasChoice(string nodeId, string optionId = null)
        {
            foreach (var e in _choiceLog)
            {
                if (e.Node != nodeId) continue;
                if (optionId == null || e.Option == optionId) return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════
        //  时间系统
        // ═══════════════════════════════════════════════════════

        /// <summary>消耗一次行动配额。返回 false 表示配额已尽。</summary>
        public bool ConsumeAction()
        {
            if (_actionsLeft <= 0) return false;
            _actionsLeft--;
            return true;
        }

        /// <summary>推进到下一天（重置行动配额与天气）</summary>
        public void AdvanceDay()
        {
            _currentDay++;
            _currentSlot = "morning";
            _actionsLeft = DialogueLoader.IsLoaded
                ? DialogueLoader.Bank.Meta?.Setting?.ActionsPerDay ?? 3
                : 3;
            _currentWeather = DefaultWeatherByDay.GetValueOrDefault(_currentDay, "clear");
        }

        public void SetSlot(string slot) => _currentSlot = slot;

        public void SetWeather(string weather) => _currentWeather = weather;

        /// <summary>直接设置当前日（模拟/调试用）</summary>
        public void SetDay(int day)
        {
            _currentDay = day;
            _currentWeather = DefaultWeatherByDay.GetValueOrDefault(day, _currentWeather);
        }
    }

    /// <summary>
    /// JSON 动态值工具：把 Newtonsoft 反序列化的 JValue 归一化为 CLR 原始值，
    /// 并提供宽松相等比较（bool / 数值 / 字符串）。
    /// </summary>
    public static class JsonValues
    {
        public static object Normalize(object value)
        {
            if (value is JValue jv) return jv.Value;
            return value;
        }

        public static bool ValueEquals(object a, object b)
        {
            a = Normalize(a);
            b = Normalize(b);

            if (a == null || b == null) return a == null && b == null;

            if (a is bool ba || b is bool bb)
            {
                if (a is bool b1 && b is bool b2) return b1 == b2;
                // 布尔与字符串 "true"/"false" 的兼容
                return string.Equals(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            if (IsNumeric(a) && IsNumeric(b))
            {
                try
                {
                    return Convert.ToDecimal(a) == Convert.ToDecimal(b);
                }
                catch { /* fallthrough */ }
            }

            return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
        }

        private static bool IsNumeric(object v)
            => v is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
