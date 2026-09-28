# 《港口宝石失窃案》台词库 — Unity 游戏程序使用指南

> 目标版本：Unity 2022.3.42f1（LTS）
> 数据源：`台词库.json`（228 条台词 + 30 个决策节点 + 4 个结局）

---

## 一、项目文件总览

| 文件 | 用途 |
|---|---|
| `台词库.json` | 完整数据文件（放入 `StreamingAssets/` 或 `Resources/`） |
| `dialogue-bank.schema.json` | JSON Schema，用于 CI 校验数据完整性 |
| `台词库数据结构设计.md` | 结构设计文档（含枚举表、条件规则、编号规范） |
| `台词库清单-全量.md` | 228 条台词 + 30 个决策节点的自动生成清单 |
| `粗略剧本.txt` | 原始剧本（7 天 3 行动模型） |

---

## 二、数据模型速览

### 2.1 顶层结构

```
DialogueBank
├── meta          — 元数据（标题、版本、设定）
├── enums         — 枚举字典
│   ├── npc[]        — 14 个 NPC 定义
│   ├── location[]   — 16 个地点定义
│   ├── timeSlot[]   — 8 个时段
│   ├── event[]      — 事件定义（含 F1/F2/F3 强制节点）
│   ├── flag[]       — 46 个 flag（main/side/hidden 命名空间）
│   └── item[]       — 25 个道具
├── lines[]       — 228 条台词/旁白/文书（核心表）
├── choiceNodes[] — 30 个 a/b/c/d 决策节点
└── endings[]     — 4 个结局分支
```

### 2.2 核心数据流

```
玩家进入场景
    ↓
DialogueManager 收集当前条件
    (day, slot, location, visitIndex, flags, items, npcState, relationship)
    ↓
遍历 lines[] 中 trigger 匹配的候选台词
    ↓
逐条求值 conditions（任一子条件不满足则淘汰）
    ↓
按 group 去重（同组只保留 priority 最高的一条）
    ↓
全局排序（priority 降序 → id 升序）
    ↓
过滤 once=true 且已播过的
    ↓
播放队首；队列为空 → idle_fallback；再无 → 静默
```

### 2.3 决策节点流

```
台词 next.type == "choice_node"
    ↓
显示 ChoiceNode.options（最多 4 个，a/b/c/d）
    ↓
检查每个 option.requires → 不满足的置灰或隐藏
    ↓
maxPicks 控制可选数量（1=单选，2=多选）
    ↓
选择后：
  1. 记录 choiceLog（只追加不修改）
  2. 应用 option.effects（flag/item/relationship）
  3. 播放 responseLines
  4. 消耗一次 pick，直到 maxPicks 用尽
    ↓
播放 onExhausted 收束台词
```

---

## 三、Unity 集成步骤

### 3.1 放置数据文件

```
Assets/
└── StreamingAssets/
    └── dialogue/
        └── dialogue_bank.json    ← 将"台词库.json"重命名放入
```

> 用 `StreamingAssets` 可在运行时用 `UnityWebRequest` 加载，支持热更新。

### 3.2 安装依赖

项目使用 `Newtonsoft.Json`（Json.NET for Unity）：

```
// Package Manager → Add package by name
com.unity.nuget.newtonsoft-json
```

### 3.3 核心类文件清单

将以下 C# 文件放入 `Assets/Scripts/Dialogue/`：

| 文件 | 职责 |
|---|---|
| `DialogueBank.cs` | 数据模型类（与 JSON 结构 1:1 映射，含 `Comparator` 比较器） |
| `DialogueLoader.cs` | JSON 加载与索引构建（按 ID / 触发类型 / 场景+日 三种索引） |
| `GameState.cs` | 运行时状态管理（flag / item / relationship / npcState / choiceLog / 存档） |
| `DialogueManager.cs` | 台词仲裁引擎（条件求值 → 时间窗口 → group 互斥 → 优先级排序） |
| `DialogueRunner.cs` | MonoBehaviour 驱动器（协程播放台词序列、多选循环、死锁兜底） |
| `DialogueSelfTest.cs` | 测试与校验库（静态校验 / 引擎逻辑 / 流程模拟，纯逻辑无 MonoBehaviour 依赖） |
| `DebugDialogueUI.cs` | 最简调试 UI（OnGUI 绘制，测试期免搭 Canvas） |
| `TestDriver.cs` | PlayMode 测试驱动台（键盘跳转日期/场景/节点） |

放入 `Assets/Editor/`（仅编辑器，不进包体）：

| 文件 | 职责 |
|---|---|
| `DialogueEditor/DialogueEditorWindow.cs` | 文案台词填写工具（菜单 Tools → 台词编辑器） |
| `DialogueEditor/DialogueTestWindow.cs` | 一键测试窗口（菜单 Tools → 台词库测试） |
| `DialogueTests/DialogueBankTests.cs` | NUnit EditMode 测试封装（供 Test Runner 与 CI 使用） |

### 3.4 关键 API

```csharp
// 初始化（游戏启动时调用一次）
DialogueLoader.Load("dialogue/dialogue_bank.json");
GameState.Init();                       // 无参，内部读 DialogueLoader.Bank

// 进入场景：返回按优先级排序的 enter_scene 台词队列
List<DialogueLine> lines = DialogueManager.EnterScene("wharf_12");

// 触发强制节点（按 eventId）
DialogueManager.TriggerForcedNode("F1_tavern_reveal");

// 触发当前场景的全部强制节点（含 event 为 null 的兜底，见测试指南缺陷 A）
DialogueManager.TriggerForcedNodeAtScene("police_station");

// 纯查询：不改变任何状态，可反复调用（测试与预览用）
DialogueManager.QuerySceneLines("tavern", "enter_scene");
DialogueManager.QueryIdleFallback("landlady", "tavern");
DialogueManager.QueryOptionStatus(node);

// 呈现决策节点
var node = DialogueManager.GetChoiceNode("CN_D1_WHARF_EVIDENCE");
DialogueManager.IsOptionAvailable(node.Options[0]);   // 选项是否可选
DialogueManager.PickOption(node, "a");                // 记录选择 + 应用效果 + 取回应台词
DialogueManager.IsNodeDeadlocked(node, out var skip); // 死锁检测

// 查看物证
DialogueManager.InspectItem("wife_letter");

// NPC 主动搭话（法官判决靠这个触发）
DialogueManager.NpcInitiative("judge", "court");

// 检查结局
var ending = DialogueManager.CheckEnding();

// 存档 / 读档
string json = GameState.Instance.ToJson();
GameState.FromJson(json);
```

> **`EnterScene` 与 `QuerySceneLines` 的区别**：前者会递增访问计数并切换当前地点（有副作用），
> 后者是纯查询。单元测试与预览工具应用后者，避免污染状态。

> 实际游戏中建议直接用 `DialogueRunner` 的高层 API（`PlayScene` / `PlayChoice` / `PlayInspectItem`），
> 它已封装好强制节点兜底、多选循环、死锁处理与 UI 调度。
> `DialogueManager` 适合需要精细控制或做自动化测试的场合。

完整的调用时机、位置与测试清单见 **`Unity测试工作指南.md`**。

---

## 四、条件系统详解

### 4.1 双层条件模型

```
第 1 层（原始层）：ChoiceLog — 忠实记录每次选择
        ↓ effects 规则实时折叠
第 2 层（派生层）：GameState — flags / items / npcState / relationship
        ↓
台词条件 99% 只查第 2 层
极少数查 choiceLog（判断顺序/精确到"哪天选了哪个"）
```

### 4.2 条件字段

| 字段 | 类型 | 含义 |
|---|---|---|
| `requiresFlags` | `string[]` | 必须拥有的 flag |
| `forbidsFlags` | `string[]` | 不能拥有的 flag |
| `requiresItems` | `string[]` | 背包必须有的道具 |
| `forbidsItems` | `string[]` | 背包不能有的道具 |
| `npcState` | `{npcId: {key: value}}` | NPC 状态断言 |
| `relationship` | `{npcId: {op: value}}` | 关系值阈值，如 `{"detective": {">=": 2}}` |
| `visitIndex` | `{op: value}` | 该场景的第几次访问 |
| `day` | `{op: value}` | 当前日 |
| `slot` | `string[]` | 当前时段 |
| `weather` | `string[]` | 当前天气 |
| `actionBudget` | `{remaining: {op: value}}` | 行动配额 |
| `chain` | `ChainCondition` | 直接查原始选择链 |

### 4.3 选择链匹配模式

| mode | 含义 | 典型用途 |
|---|---|---|
| `all` | 列出的选择全发生过（不论顺序） | "既去过海关又去过警局" |
| `any` | 至少发生过一条 | "用任意方式得知了冷藏库" |
| `ordered_all` | 全部发生且顺序一致 | "先从老板娘处拿纸条、再去警局" |
| `ordered_any` | 按序命中若干 | — |
| `count` | 命中数 ≥ `minCount` | "搜集到 3 条以上关于律师的证据" |

### 4.4 多条命中仲裁

```
1. 收集 trigger 匹配的候选台词
2. 逐条求值 conditions（任一子条件不满足则淘汰）
3. 按 group 去重：同组只保留 priority 最高的一条
4. 全局按 priority 降序、再按 id 升序
5. 过滤 once=true 且已播过的
6. 播放队首；空 → idle_fallback；再无 → 静默
```

### 4.5 Priority 建议区间

| 区间 | 用途 |
|---|---|
| 100 | 强制节点 F1/F2/F3 |
| 80 | 主线关键推进 |
| 60 | 选项回应 |
| 40 | 状态变体 / 暗线 |
| 30 | 重复访问递进情报 |
| 10 | 兜底闲聊 |

---

## 五、Effect 系统

台词条目或选项播放后，通过 `effects` 修改游戏状态：

| 字段 | 作用 |
|---|---|
| `setFlags` | 设置 flag（`hidden.*` 不可逆，只能设不能清） |
| `clearFlags` | 清除 flag（禁止清除 `hidden.*`） |
| `addItems` | 道具加入背包 |
| `removeItems` | 道具移出背包 |
| `relationship` | `{npcId: 增量}` 如 `{"detective": 1}` |
| `npcState` | 修改 NPC 状态 |
| `unlockNodes` | 解锁决策节点 |
| `advanceDay` | 推进到下一天 |
| `consumeAction` | 消耗一次行动配额 |
| `teleport` | 传送到指定场景 |
| `triggerEvent` | 触发事件 |

---

## 六、存档结构

```jsonc
{
  "saveId": "…",
  "scriptVersion": "1.0",
  "sys": {
    "day": 4,
    "slot": "afternoon",
    "actionsLeft": 2,
    "weather": "rainy"
  },
  "choiceLog": [
    { "day": 1, "node": "CN_D1_WHARF_EVIDENCE", "option": "a" },
    { "day": 1, "node": "CN_D1_WHARF_EVIDENCE", "option": "d" },
    { "day": 2, "node": "CN_D2_POLICE_TALK",    "option": "c" }
  ],
  "visitCount": { "wharf_12": 3, "tavern": 2 },
  "flags": ["main.found_pink_shards", "main.registry_gap", "hidden.photo_acquired"],
  "inventory": ["pink_shard", "old_silver_photo", "seal_ring"],
  "npcState": {
    "detective":   { "trust": 3, "present": false },
    "young_noble": { "trust": 2, "present": true },
    "lawyer":      { "exposed": false }
  },
  "playedLines": ["DL_NOB_MANSION_D4_010", "…"],
  "currentNode": "CN_D4_MANSION_TALK"
}
```

**`choiceLog` 只追加、永不修改** —— 它是 `conditions.chain` 的数据源，也是调试依据。

---

## 七、枚举速查

### NPC（14 个）

| id | 中文名 | 口癖 | 角色定位 |
|---|---|---|---|
| `young_noble` | 年轻贵族 | 无 | 委托方 + 身世线 |
| `detective` | 警探 | **句首"嗯"** | 可靠盟友 |
| `dock_manager` | 码头经理 | 无 | 执行者 |
| `lawyer` | 律师 | 无 | 幕后主使 |
| `landlady` | 破锚酒馆老板娘 | 无 | 情报总源 |
| `editor` | 报社主编 | 无 | 暗线人物 |
| `manager_wife` | 码头经理妻子 | 无 | 关键证人 |
| `lawyer_clerk` | 律师秘书 | 无 | 信息漏口 |
| `dock_worker` | 码头工人 | 无 | 群像目击 |
| `customs_officer` | 海关职员 | 无 | 文书提供 |
| `police_officer` | 警员 | 无 | 跑腿转交 |
| `judge` | 法官 | 无 | 程序裁决 |
| `old_butler` | 贵族家老管家 | 无 | 支线（offscreen） |
| `narrator` | 旁白/记者内心 | 无 | 第一人称观察 |

### 地点（16 个）

| id | 中文名 |
|---|---|
| `wharf_12` | 12号码头仓库 |
| `wharf_alley` | 码头后巷 |
| `cold_storage` | 废弃冷藏库 |
| `tunnel` | 地下隧道 |
| `tavern` | 破锚酒馆 |
| `mansion_study` | 贵族宅邸书房 |
| `newsroom` | 报社 |
| `police_station` | 码头警局 |
| `customs_house` | 海关大楼 |
| `manager_office` | 码头经理办公室 |
| `lawyer_office` | 律师办公室 |
| `hotel_swan` | 天鹅与皇冠旅馆 |
| `park_station` | 皇后公园站储物柜 |
| `court` | 法庭 |
| `court_corridor` | 法庭走廊 |
| `kent_attic` | 肯特郡阁楼 |

### 时段

`dawn` → `morning` → `noon` → `afternoon` → `dusk` → `night` → `midnight` → `any`

---

## 八、重要实现提醒

### 8.1 警探的"嗯"不要写进 text

口癖已定义在 `enums.npc[detective].verbalTic = "嗯"`，由渲染层统一前置拼接：

```csharp
string displayText = line.Text;
var npcDef = DialogueLoader.Bank.Enums.Npc.Find(n => n.Id == line.Npc);
if (npcDef?.VerbalTic != null && !string.IsNullOrEmpty(displayText))
    displayText = npcDef.VerbalTic + "，" + displayText;
```

### 8.2 带 requires 的多选节点必须有出路

`CN_D7_COURT_EVIDENCE` 的四个选项全部带前置条件。如果玩家前六天什么证据都没拿到，到了法庭四个选项全灰 → 死锁。已通过 `allowSkip + skipLine` 解决。

### 8.3 D7 时序陷阱

十二封信在**庭后**才到手（D7 行动②），不能用于庭审条件（D7 行动①）。法庭举证只能引用：妻子的信、经理供词、"已付清"字条、戴面纱女子身份。

### 8.4 group 互斥比 priority 更重要

108 条台词带 group。变体台词如果只靠优先级，条件写松就会连播。同组互斥是保险。

### 8.5 变体组内必须有恒真兜底

`DL_LAW_COURT_D7_041` 和 `DL_JDG_COURT_D7_003` 分别保证律师庭审和法官判决不会空场。

### 8.6 每天 3 次行动配额

`sys.actionsLeft` 初始为 3，每次选择带 `consumeAction: true` 的选项后 -1。配额为 0 时推进到下一时段或下一天。

---

## 九、结局判定

D7 行动全部完成后调用 `CheckEnding()`。它**按 `endings` 数组顺序**取第一个条件命中的结局，
因此数组顺序就是优先级。当前 JSON 中的实际顺序：

| 数组下标 | 结局 ID | 条件 | 正典 |
|---|---|---|---|
| 0 | `END_CANON` | 需 `main.diamond_recovered` + `main.lawyer_convicted` | 是 |
| 1 | `END_BAIL` | 需 `main.diamond_recovered` + `main.lawyer_bailed` | 是 |
| 2 | `END_LOSS` | 禁 `main.diamond_recovered` | 否 |
| 3 | `END_WIFE_TESTIFY` | 需 `hidden.wife_testified` + `main.diamond_recovered` | 否 |

> ⚠️ **已知缺陷：`END_WIFE_TESTIFY` 当前永久不可达。**
>
> `judge_verdict` 互斥组含恒真兜底 `DL_JDG_COURT_D7_003`，判决 flag
> （`main.lawyer_convicted` 或 `main.lawyer_bailed`）**必然二选一置位**。
> 于是只要 `main.diamond_recovered` 为真，下标 0 或 1 必定先命中，
> 排在末位的 `END_WIFE_TESTIFY` 永远轮不到。实测 300 次模拟中，
> 即便 `hidden.wife_testified` 已置位，结局仍报 `END_CANON`。
>
> 修法二选一：把 `END_WIFE_TESTIFY` 前移到数组首位；或将其从 `endings` 移除，
> 改为在 `END_CANON` 的 `lines` 里按该 flag 条件插入 `DL_WIF_COURT_D7_020`（该台词已存在）。
>
> 自测项 **A15** 会持续报告此问题，修好后自动转 PASS。
> 注意 `END_LOSS` **不构成**遮蔽——它 `forbidsFlags: main.diamond_recovered`，
> 与 `END_WIFE_TESTIFY` 的 requires 互斥。

---

## 十、文案填写工具

本项目附带 Unity Editor 工具 `DialogueEditorWindow`，供文案策划直接在 Unity 中填写台词：

- 菜单路径：**Tools → 台词编辑器**
- 功能：浏览全部 228 条台词，按 NPC/日/地点/类型筛选，直接编辑 `text` 字段并保存
- 口癖提示：选中警探台词时会自动提示「嗯」由渲染层前置，避免文案重复写入
- 保存：Ctrl+S 或点「保存此条」，直接写回原 JSON 文件
- 详见 `Assets/Editor/DialogueEditor/DialogueEditorWindow.cs`

> `text` 与 `summary` 分离是有意设计：文案按 `summary` 填 `text`，
> 程序在 `text` 全空时即可跑通全流程做逻辑验证。请保留这个分工。

---

## 十一、测试体系

本项目自带完整测试能力，分四层递进，**下层不过不要测上层**：

| 层 | 内容 | 入口 | 是否需进游戏 |
|---|---|---|---|
| 1 | 静态数据校验（A 组 16 项） | Tools → 台词库测试 | 否 |
| 2 | 引擎逻辑测试（B 组 11 项） | 同上 | 否 |
| 3 | 流程模拟（C/D 组，含 300 次随机通关） | 同上 | 否 |
| 4 | PlayMode 手工验证（E 组 12 项） | 挂 `DebugDialogueUI` + `TestDriver` 后 Play | 是 |

前三层封装在 `DialogueSelfTest.cs`（纯逻辑，无 MonoBehaviour 依赖），可一键运行，也可被 NUnit 直接调用以接入 CI。

第 4 层用 `TestDriver` 的键盘驱动台快速跳转日期与场景，免去"为测 D7 法庭从头玩七天"。

> **完整测试指南见 `Unity测试工作指南.md`** —— 含每个 API 的调用时机与位置、
> 全部测试清单与通过标准、5 个已发现缺陷的分析与修复记录、CI 集成方式与实测基线数据。
