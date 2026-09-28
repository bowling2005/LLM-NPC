using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dialogue
{
    // ═══════════════════════════════════════════════════════
    //  顶层根结构
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class DialogueBank
    {
        [JsonProperty("meta")]         public Meta          Meta;
        [JsonProperty("enums")]        public Enums         Enums;
        [JsonProperty("lines")]        public List<DialogueLine> Lines = new();
        [JsonProperty("choiceNodes")]  public List<ChoiceNode>   ChoiceNodes = new();
        [JsonProperty("endings")]      public List<Ending>       Endings = new();
    }

    // ═══════════════════════════════════════════════════════
    //  Meta
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Meta
    {
        [JsonProperty("title")]           public string  Title;
        [JsonProperty("schemaVersion")]   public string  SchemaVersion;
        [JsonProperty("scriptVersion")]   public string  ScriptVersion;
        [JsonProperty("setting")]         public Setting Setting;
        [JsonProperty("playerInteraction")] public string PlayerInteraction;
        [JsonProperty("note")]            public string  Note;
    }

    [Serializable]
    public class Setting
    {
        [JsonProperty("era")]           public string Era;
        [JsonProperty("location")]      public string Location;
        [JsonProperty("days")]          public int    Days;
        [JsonProperty("actionsPerDay")] public int    ActionsPerDay;
    }

    // ═══════════════════════════════════════════════════════
    //  Enums — 枚举字典
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Enums
    {
        [JsonProperty("npc")]      public List<NpcDef>      Npc = new();
        [JsonProperty("location")] public List<LocationDef> Location = new();
        [JsonProperty("timeSlot")] public List<string>      TimeSlot = new();
        [JsonProperty("event")]    public List<EventDef>    Event = new();
        [JsonProperty("flag")]     public List<FlagDef>     Flag = new();
        [JsonProperty("item")]     public List<ItemDef>     Item = new();
    }

    [Serializable]
    public class NpcDef
    {
        [JsonProperty("id")]           public string       Id;
        [JsonProperty("code")]         public string       Code;
        [JsonProperty("role")]         public string       Role;
        [JsonProperty("displayName")]  public string       DisplayName;
        [JsonProperty("age")]          public int?         Age;
        [JsonProperty("speechTraits")] public List<string> SpeechTraits = new();
        [JsonProperty("verbalTic")]    public string       VerbalTic;
        [JsonProperty("locations")]    public List<string> Locations = new();
        [JsonProperty("stateMachine")] public List<string> StateMachine = new();
    }

    [Serializable]
    public class LocationDef
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("code")]        public string Code;
        [JsonProperty("name")]        public string Name;
        [JsonProperty("function")]    public string Function;
        [JsonProperty("enterable")]   public bool   Enterable = true;
        [JsonProperty("revisitable")] public bool   Revisitable = true;
    }

    [Serializable]
    public class EventDef
    {
        [JsonProperty("id")]       public string Id;
        [JsonProperty("day")]      public int?   Day;
        [JsonProperty("location")] public string Location;
        [JsonProperty("forced")]   public bool   Forced;
        [JsonProperty("skipCost")] public string SkipCost;
    }

    [Serializable]
    public class FlagDef
    {
        [JsonProperty("id")]           public string       Id;
        [JsonProperty("namespace")]    public string       Namespace;
        [JsonProperty("desc")]         public string       Desc;
        [JsonProperty("irreversible")] public bool         Irreversible;
        [JsonProperty("setBy")]        public List<string> SetBy = new();
    }

    [Serializable]
    public class ItemDef
    {
        [JsonProperty("id")]             public string Id;
        [JsonProperty("name")]           public string Name;
        [JsonProperty("kind")]           public string Kind;
        [JsonProperty("inspectLine")]    public string InspectLine;
        [JsonProperty("chainRelevance")] public string ChainRelevance;
    }

    // ═══════════════════════════════════════════════════════
    //  DialogueLine — 台词条目（核心）
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class DialogueLine
    {
        [JsonProperty("id")]         public string   Id;
        [JsonProperty("type")]       public string   Type;      // dialogue | narration | document | inner
        [JsonProperty("version")]    public string   Version;
        [JsonProperty("npc")]        public string   Npc;
        [JsonProperty("target")]     public string   Target = "player";
        [JsonProperty("location")]   public string   Location;
        [JsonProperty("time")]       public TimeInfo Time;
        [JsonProperty("event")]      public string   Event;
        [JsonProperty("trigger")]    public Trigger  Trigger;
        [JsonProperty("conditions")] public Conditions Conditions;
        [JsonProperty("priority")]   public int      Priority;
        [JsonProperty("once")]       public bool     Once;
        [JsonProperty("group")]      public string   Group;
        [JsonProperty("index")]      public int?     Index;
        [JsonProperty("text")]       public string   Text = "";
        [JsonProperty("summary")]    public string   Summary;
        [JsonProperty("delivery")]   public Delivery Delivery;
        [JsonProperty("effects")]    public Effects  Effects;
        [JsonProperty("next")]       public Next     Next;
        [JsonProperty("fallback")]   public string   Fallback;
        [JsonProperty("tags")]       public List<string> Tags = new();
        [JsonProperty("notes")]      public string   Notes;
    }

    [Serializable]
    public class TimeInfo
    {
        [JsonProperty("day")]        public int?         Day;
        [JsonProperty("slot")]       public List<string> Slot = new();
        [JsonProperty("weather")]    public string       Weather;
        [JsonProperty("visitIndex")] public int?         VisitIndex;
    }

    [Serializable]
    public class Trigger
    {
        [JsonProperty("type")]         public string Type;
        [JsonProperty("nodeId")]       public string NodeId;
        [JsonProperty("choiceNodeId")] public string ChoiceNodeId;
        [JsonProperty("optionId")]     public string OptionId;
        [JsonProperty("itemId")]       public string ItemId;
        [JsonProperty("eventId")]      public string EventId;
    }

    [Serializable]
    public class Next
    {
        [JsonProperty("type")] public string Type;  // line | choice_node | scene | ending | auto | none
        [JsonProperty("id")]   public string Id;
    }

    // ═══════════════════════════════════════════════════════
    //  Comparator — 数值比较器
    //  对应 JSON 形如 {">=": 2} / {"==": 3} / {"in": [1,2]}
    //  至多一个键（schema 约束），实现为全部可空、取非空者
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Comparator
    {
        [JsonProperty("==")]  public decimal?   Eq;
        [JsonProperty("!=")]  public decimal?   Ne;
        [JsonProperty(">=")]  public decimal?   Ge;
        [JsonProperty("<=")]  public decimal?   Le;
        [JsonProperty(">")]   public decimal?   Gt;
        [JsonProperty("<")]   public decimal?   Lt;
        [JsonProperty("in")]  public List<decimal> In;

        public bool IsEmpty =>
            Eq == null && Ne == null && Ge == null && Le == null &&
            Gt == null && Lt == null && (In == null || In.Count == 0);

        /// <summary>判断实际值是否满足本比较器。空比较器视为恒真。</summary>
        public bool Satisfies(int actual)
        {
            decimal v = actual;

            if (Eq.HasValue && v != Eq.Value) return false;
            if (Ne.HasValue && v == Ne.Value) return false;
            if (Ge.HasValue && v <  Ge.Value) return false;
            if (Le.HasValue && v >  Le.Value) return false;
            if (Gt.HasValue && v <= Gt.Value) return false;
            if (Lt.HasValue && v >= Lt.Value) return false;
            if (In != null && In.Count > 0 && !In.Contains(v)) return false;

            return true;
        }

        public override string ToString()
        {
            if (Eq.HasValue) return $"== {Eq}";
            if (Ne.HasValue) return $"!= {Ne}";
            if (Ge.HasValue) return $">= {Ge}";
            if (Le.HasValue) return $"<= {Le}";
            if (Gt.HasValue) return $"> {Gt}";
            if (Lt.HasValue) return $"< {Lt}";
            if (In != null)  return $"in [{string.Join(",", In)}]";
            return "(恒真)";
        }
    }

    // ═══════════════════════════════════════════════════════
    //  Conditions — 条件组（所有子条件为 AND 关系）
    //  结构与 台词库.json 实际数据对齐：
    //    relationship: { npcId: { 维度: { 操作符: 数值 } } }
    //    npcState:     { npcId: { 键: 布尔值 } }
    //    visitIndex / day / actionBudget.remaining: Comparator
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Conditions
    {
        [JsonProperty("requiresFlags")] public List<string> RequiresFlags = new();
        [JsonProperty("forbidsFlags")]  public List<string> ForbidsFlags = new();
        [JsonProperty("requiresItems")] public List<string> RequiresItems = new();
        [JsonProperty("forbidsItems")]  public List<string> ForbidsItems = new();

        /// <summary>{ npcId: { stateKey: 期望值 } }，值可能是 bool/string/number（JValue 归一化后比较）</summary>
        [JsonProperty("npcState")]
        public Dictionary<string, Dictionary<string, object>> NpcState;

        /// <summary>{ npcId: { "trust"|"hostility": Comparator } }</summary>
        [JsonProperty("relationship")]
        public Dictionary<string, Dictionary<string, Comparator>> Relationship;

        [JsonProperty("visitIndex")]   public Comparator VisitIndex;
        [JsonProperty("day")]          public Comparator Day;
        [JsonProperty("slot")]         public List<string> Slot;
        [JsonProperty("weather")]      public List<string> Weather;
        [JsonProperty("actionBudget")] public ActionBudgetCond ActionBudget;
        [JsonProperty("chain")]        public ChainCondition Chain;

        /// <summary>条件组是否为空（无任何约束 → 恒真）</summary>
        public bool IsEmpty =>
            (RequiresFlags == null || RequiresFlags.Count == 0) &&
            (ForbidsFlags  == null || ForbidsFlags.Count  == 0) &&
            (RequiresItems == null || RequiresItems.Count == 0) &&
            (ForbidsItems  == null || ForbidsItems.Count  == 0) &&
            (NpcState == null || NpcState.Count == 0) &&
            (Relationship == null || Relationship.Count == 0) &&
            (VisitIndex == null || VisitIndex.IsEmpty) &&
            (Day == null || Day.IsEmpty) &&
            (Slot == null || Slot.Count == 0) &&
            (Weather == null || Weather.Count == 0) &&
            ActionBudget == null &&
            Chain == null;
    }

    [Serializable]
    public class ActionBudgetCond
    {
        [JsonProperty("remaining")] public Comparator Remaining;
    }

    [Serializable]
    public class ChainCondition
    {
        [JsonProperty("mode")]       public string           Mode;  // all | any | ordered_all | ordered_any | count
        [JsonProperty("entries")]    public List<ChainEntry> Entries = new();
        [JsonProperty("minCount")]   public int?             MinCount;
        [JsonProperty("withinDays")] public List<int>        WithinDays;
    }

    [Serializable]
    public class ChainEntry
    {
        [JsonProperty("node")]   public string Node;
        [JsonProperty("option")] public string Option;  // a | b | c | d | null
        [JsonProperty("day")]    public int?   Day;
    }

    // ═══════════════════════════════════════════════════════
    //  Effects — 效果
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Effects
    {
        [JsonProperty("setFlags")]      public List<string>            SetFlags = new();
        [JsonProperty("clearFlags")]    public List<string>            ClearFlags = new();
        [JsonProperty("addItems")]      public List<string>            AddItems = new();
        [JsonProperty("removeItems")]   public List<string>            RemoveItems = new();
        [JsonProperty("relationship")]  public Dictionary<string, int> Relationship;
        /// <summary>{ npcId: { stateKey: 值 } }，如 { "young_noble": { "present": false } }</summary>
        [JsonProperty("npcState")]
        public Dictionary<string, Dictionary<string, object>> NpcState;
        [JsonProperty("unlockNodes")]   public List<string>            UnlockNodes = new();
        [JsonProperty("advanceDay")]    public bool                    AdvanceDay;
        [JsonProperty("consumeAction")] public bool                    ConsumeAction;
        [JsonProperty("teleport")]      public string                  Teleport;
        [JsonProperty("triggerEvent")]  public string                  TriggerEvent;

        public bool IsEmpty =>
            (SetFlags == null || SetFlags.Count == 0) &&
            (ClearFlags == null || ClearFlags.Count == 0) &&
            (AddItems == null || AddItems.Count == 0) &&
            (RemoveItems == null || RemoveItems.Count == 0) &&
            (Relationship == null || Relationship.Count == 0) &&
            (NpcState == null || NpcState.Count == 0) &&
            (UnlockNodes == null || UnlockNodes.Count == 0) &&
            !AdvanceDay && !ConsumeAction &&
            string.IsNullOrEmpty(Teleport) && string.IsNullOrEmpty(TriggerEvent);
    }

    // ═══════════════════════════════════════════════════════
    //  Delivery — 演出层
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Delivery
    {
        [JsonProperty("emotion")]  public string Emotion;
        [JsonProperty("tone")]     public string Tone;
        [JsonProperty("action")]   public string Action;
        [JsonProperty("sfx")]      public string Sfx;
        [JsonProperty("bgm")]      public string Bgm;
        [JsonProperty("portrait")] public string Portrait;
        [JsonProperty("render")]   public string Render = "speech_bubble";
        [JsonProperty("camera")]   public string Camera;
    }

    // ═══════════════════════════════════════════════════════
    //  ChoiceNode — a/b/c/d 决策节点
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class ChoiceNode
    {
        [JsonProperty("id")]          public string             Id;
        [JsonProperty("day")]         public int                Day;
        [JsonProperty("slot")]        public string             Slot;
        [JsonProperty("location")]    public string             Location;
        [JsonProperty("event")]       public string             Event;
        [JsonProperty("promptLine")]  public string             PromptLine;
        [JsonProperty("maxPicks")]    public int                MaxPicks = 1;
        [JsonProperty("allowSkip")]   public bool               AllowSkip;
        [JsonProperty("skipLine")]    public string             SkipLine;
        [JsonProperty("repeatable")]  public bool               Repeatable;
        [JsonProperty("conditions")]  public Conditions         Conditions;
        [JsonProperty("options")]     public List<ChoiceOption> Options = new();
        [JsonProperty("onExhausted")] public string             OnExhausted;
        [JsonProperty("onTimeout")]   public string             OnTimeout;
    }

    [Serializable]
    public class ChoiceOption
    {
        [JsonProperty("id")]            public string       Id;  // a | b | c | d
        [JsonProperty("label")]         public string       Label = "";
        [JsonProperty("summary")]       public string       Summary;
        [JsonProperty("requires")]      public Conditions   Requires;
        [JsonProperty("hidden")]        public bool         Hidden;
        [JsonProperty("consumable")]    public bool         Consumable = true;
        [JsonProperty("responseLines")] public List<string> ResponseLines = new();
        [JsonProperty("effects")]       public Effects      Effects;
        [JsonProperty("next")]          public Next         Next;
    }

    // ═══════════════════════════════════════════════════════
    //  Ending — 结局分支
    // ═══════════════════════════════════════════════════════

    [Serializable]
    public class Ending
    {
        [JsonProperty("id")]         public string       Id;
        [JsonProperty("name")]       public string       Name;
        [JsonProperty("conditions")] public Conditions   Conditions;
        [JsonProperty("lines")]      public List<string> Lines = new();
        [JsonProperty("isCanon")]    public bool         IsCanon;
    }
}
