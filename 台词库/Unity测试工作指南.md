# 《港口宝石失窃案》台词库 — Unity 测试工作指南

> 目标版本：Unity 2022.3.42f1（LTS）
> 配套代码：`UnityScripts/Dialogue/`（6 个运行时脚本）+ `UnityScripts/Editor/DialogueEditor/`（2 个编辑器工具）

---

## 一、测试总体策略：三层递进

台词库的测试不是一次性的，它随开发阶段推进，分三层做，每层解决的问题不同：

```
第 1 层 静态数据校验（不跑游戏，纯数据体检）
   ↓ 数据没问题了，才值得测逻辑
第 2 层 引擎逻辑测试（不跑游戏，验条件求值/仲裁/存档）
   ↓ 引擎对了，才值得跑流程
第 3 层 流程模拟（跑通七日全流程，验死锁与结局可达）
   ↓ 模拟过了，才值得上手玩
第 4 层 PlayMode 手工验证（真 UI 真交互，人工体验）
```

**关键原则：下层不过不要测上层。** 数据里有个悬空引用，你在 PlayMode 里会看到"NPC 不说话"，然后花两小时去查引擎——其实是数据写错了。静态校验 3 秒就能告诉你。

这三层已经封装进 `DialogueSelfTest.cs`，对应测试报告的 A / B / C / D 四组：

| 组 | 名称 | 对应层 | 是否需要进入游戏 | 耗时 |
|---|---|---|---|---|
| **A** | 静态数据校验 | 第 1 层 | 否 | < 1 秒 |
| **B** | 引擎逻辑测试 | 第 2 层 | 否 | < 1 秒 |
| **C** | 流程模拟 | 第 3 层 | 否 | 数秒 |
| **D** | 定向结局 | 第 3 层补充 | 否 | < 1 秒 |

第 4 层 PlayMode 手工验证需要你搭好 UI，见第六节。

---

## 二、最快上手：编辑器一键测试

不需要装 Unity Test Framework，也不需要写任何代码。

```
Unity 菜单 → Tools → 台词库测试
```

打开窗口后：

1. **台词库 JSON** 路径通常已自动定位；若未找到，点「浏览」手动选择 `台词库.json`
2. 勾选要跑的分组（A/B/C/D，默认全选）
3. 点「运行全部测试」
4. 结果按 PASS（绿）/ WARN（黄）/ FAIL（红）分类展示，点击条目可展开明细
5. 点「导出报告」存为 txt 交给团队

> 「仅静态校验」等按钮用于单独跑某一组，改完数据想快速验证时比全跑快。

---

## 三、API 调用时机与位置速查

这是本指南的核心：**什么时候、在哪里、调哪个 API**。

### 3.1 游戏启动（一次）

| 时机 | 位置 | 调用 |
|---|---|---|
| 游戏管理器 `Awake()` | `GameBootstrapper.cs`（你的脚本） | `DialogueLoader.Load("dialogue/dialogue_bank.json")` |
| 紧接着 | 同上 | `GameState.Init()` |
| 然后 | 同上 | `runner.Init(myDialogueUI)` |

```csharp
public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private DialogueRunner runner;
    [SerializeField] private MyDialogueUI ui;   // 你实现的 IDialogueUI

    void Awake()
    {
        runner.Init(ui);   // 内部已完成 Load + GameState.Init
    }
}
```

> `DialogueRunner.Init()` 内部已经调用了 `DialogueLoader.Load` 和 `GameState.Init`，不必重复调用。
> 若你想分开控制（比如先加载数据再延迟初始化状态），才需要手动调那两个。

### 3.2 玩家进入场景

| 时机 | 位置 | 调用 |
|---|---|---|
| 场景加载完成、玩家角色就位后 | 场景控制器 / 传送点触发器 | `runner.PlayScene("wharf_12")` |

```csharp
// 场景入口触发器
void OnTriggerEnter(Collider other)
{
    if (other.CompareTag("Player"))
        runner.PlayScene("wharf_12");
}
```

`PlayScene` 内部做了三件事，**顺序很重要**：
1. `GameState.EnterScene(location)` — 递增访问计数、切换当前地点
2. `TriggerForcedNodeAtScene(location)` — 先播该场景的强制节点台词
3. `QuerySceneLines(location)` — 再播进场台词

> 第 2 步不能省：有 4 条 forced_node 台词的 `event` 字段为 null（见第五节缺陷 A），
> 只靠 `PlayForcedNode(eventId)` 永远命中不了它们。

### 3.3 触发强制节点（F1 / F2 / F3）

| 时机 | 位置 | 调用 |
|---|---|---|
| 到达剧本规定的剧情时点 | 日程系统 / 剧情触发器 | `runner.PlayForcedNode("F1_tavern_reveal")` |

三个强制节点的 eventId：

| 节点 | eventId | 触发时点 | 地点 |
|---|---|---|---|
| F1 | `F1_tavern_reveal` | D1 傍晚 | `tavern` |
| F2 | `F2_mansion_photo` | D4 前往宅邸时 | `mansion_study` |
| F3 | `F3_court` | D7 法庭 | `court` |

```csharp
// 日程推进到 D1 傍晚，且玩家在酒馆
if (day == 1 && slot == "dusk" && currentLocation == "tavern")
    runner.PlayForcedNode("F1_tavern_reveal");
```

> **优先用 `PlayScene`**，它已内含场景级强制节点兜底。
> `PlayForcedNode(eventId)` 用于跨场景的事件触发，或你明确知道 eventId 的场合。

### 3.4 呈现决策节点（a/b/c/d 选项）

| 时机 | 位置 | 调用 |
|---|---|---|
| 台词 `next.type == "choice_node"`，或场景交互点 | 对话流程中自动，或交互触发器 | `runner.PlayChoice("CN_D1_WHARF_EVIDENCE")` |

大多数情况**不需要手动调**：台词的 `next` 字段指向 `choice_node` 时，`DialogueRunner` 会自动接着播。

需要手动调的场景：场景里的可交互物件（如"调查货箱"按钮）直接开一个决策节点。

```csharp
// 点击码头货箱
void OnInspectCargo()
{
    runner.PlayChoice("CN_D1_WHARF_EVIDENCE", (node, picked) =>
    {
        Debug.Log($"玩家在 {node.Id} 最后选了 {picked}");
        // 这里可以关闭调查 UI、恢复玩家操作等
    });
}
```

`PlayChoice` 内部已处理：
- 播 `promptLine` 引导台词
- 死锁检测（全选项不可用时报错，有 skipLine 时走兜底）
- **多选循环**：`maxPicks=2` 的节点会连续让玩家选 2 次，已选过的 `consumable` 选项不再出现
- 每次选择后播 `responseLines`，全部选完播 `onExhausted` 收束

### 3.5 查看物证 / 文书

| 时机 | 位置 | 调用 |
|---|---|---|
| 玩家点开背包里的证据 | 背包 UI 的"查看"按钮 | `runner.PlayInspectItem("wife_letter")` |

```csharp
// 背包项点击查看
void OnInventoryItemClick(string itemId)
{
    runner.PlayInspectItem(itemId);   // 播出该物证的 document 类台词
}
```

22 条 `item_inspect` 台词靠这个触发。itemId 清单见 `台词库.json` 的 `enums.item`。

### 3.6 NPC 主动搭话

| 时机 | 位置 | 调用 |
|---|---|---|
| 玩家在 NPC 附近停留、或关系值达阈值 | NPC AI 脚本 | `DialogueManager.NpcInitiative("judge", "court")` |

```csharp
// 法官在庭审中依状态主动宣判
var lines = DialogueManager.NpcInitiative("judge", "court");
if (lines.Count > 0)
    runner.PlayLines(lines);
```

> 39 条 `npc_initiative` 台词。**法官的两条判决台词就靠这个触发**——
> 它们属 `judge_verdict` 互斥组，引擎会择一播出并置位 `main.lawyer_convicted` 或 `main.lawyer_bailed`。
> 不调这个方法，结局判定就永远拿不到判决 flag。

### 3.7 结局判定

| 时机 | 位置 | 调用 |
|---|---|---|
| D7 全部行动结束 | 日程系统的第七天收尾 | `DialogueManager.CheckEnding()` |

```csharp
void OnDay7Complete()
{
    var ending = DialogueManager.CheckEnding();
    if (ending != null)
    {
        runner.PlayLines(ending.Lines.Select(id => DialogueLoader.GetLine(id))
                                        .Where(l => l != null).ToList());
        ShowEndingScreen(ending.Name);   // 你的结算界面
    }
}
```

### 3.8 存档 / 读档

| 时机 | 位置 | 调用 |
|---|---|---|
| 玩家保存 | 存档 UI | `GameState.Instance.ToJson()` → 写文件 |
| 玩家读档 | 存档 UI | `GameState.FromJson(json)` |

```csharp
// 存档
string json = GameState.Instance.ToJson();
File.WriteAllText(SavePath(slot), json);

// 读档
string json = File.ReadAllText(SavePath(slot));
GameState.FromJson(json);        // 自动设为当前实例
runner.PlayScene(GameState.Instance.CurrentLocation);
```

> 存档含 `choiceLog`（原始选择链）。**只追加、永不修改**——它是 `conditions.chain` 的数据源，也是排查"这句台词为什么没触发"的唯一依据。

### 3.9 API 调用全景图

```
启动
 └─ DialogueRunner.Init(ui)          ← 内部：Load + GameState.Init

进入场景
 └─ DialogueRunner.PlayScene(loc)    ← 内部：EnterScene + 强制节点 + 进场台词 + 兜底
      ├─ 台词 next=choice_node → PlayChoice 自动接续
      ├─ 台词 next=ending      → CheckEnding 自动接续
      └─ 播完 → UI 恢复玩家操作

日程推进到强制时点
 └─ DialogueRunner.PlayForcedNode(eventId)

玩家交互
 ├─ 点物证      → PlayInspectItem(itemId)
 ├─ 点交互物    → PlayChoice(nodeId)
 └─ NPC 搭话    → DialogueManager.NpcInitiative(npc, loc) + PlayLines

D7 收尾
 └─ DialogueManager.CheckEnding() → PlayLines(ending.Lines)

任意时刻
 ├─ 存档 → GameState.Instance.ToJson()
 └─ 读档 → GameState.FromJson(json)
```

---

## 四、必须完成的测试清单

### 4.1 第 1 层 · 静态数据校验（A 组，16 项）

改完数据必跑。全部为自动检查，无需人工判断。

| 编号 | 检查项 | 失败意味着 |
|---|---|---|
| A1 | 数据规模 | 文件损坏或加载错误 |
| A2 | ID 唯一性 | 索引会互相覆盖，台词随机丢失 |
| A3 | ID 格式规范 | 编号混乱，后续按规则查找失效 |
| A4 | 枚举引用完整性 | 条件引用了不存在的 flag/item/npc → 条件恒假，台词永不触发 |
| A5 | 悬空引用 | `next` / `promptLine` / `responseLines` 指向不存在的 ID → 剧情中断 |
| A6 | **死条件** | 被条件引用但无处置位的 flag → 对应分支永远进不去 |
| A7 | flag 利用率（警告） | 已置位但无人引用的 flag，是给文案预留的钩子，非错误 |
| A8 | 兜底覆盖（警告） | 某 NPC 在某场景缺 `idle_fallback` → 玩家可能面对沉默的 NPC |
| A9 | 节点出路 | 全选项带前置条件且无 skipLine → 玩家卡死 |
| A10 | group 恒真兜底（警告） | 互斥组无恒真成员 → 变体全落选时该组静默 |
| A11 | 强制节点 F1/F2/F3 | 强制节点未绑定台词或 priority ≠ 100 |
| A12 | forced_node 事件绑定（警告） | event 为 null，需依赖场景级兜底触发 |
| A13 | 时段声明一致性（警告） | 台词 slot 与节点 slot 不符，数据卫生问题 |
| A14 | 选项结构 | 选项 ID 非法/重复、数量超 2~4、maxPicks 超选项数 |
| A15 | **结局遮蔽** | 靠后的结局条件被前面覆盖 → 该结局永远不可达 |
| A16 | 判决组恒真兜底 | 产出结局 flag 的互斥组无恒真成员 → 相关结局不可达 |

> A6、A15、A16 是三项最有价值的检查——它们抓的都是**肉眼绝对看不出来**的逻辑死路。

### 4.2 第 2 层 · 引擎逻辑测试（B 组，11 项）

改完引擎代码必跑。

| 编号 | 检查项 | 验证内容 |
|---|---|---|
| B1 | Comparator 操作符 | `>= <= > < == != in` 七种比较全部正确 |
| B2 | flag 条件与不可逆 | requires/forbids 生效；`hidden.*` 拒绝清除 |
| B3 | 关系值 | 增量、0~5 钳制、阈值比较 |
| B4 | 道具条件 | requiresItems / forbidsItems |
| B5 | **时间窗口** | day/slot 过滤；显式触发豁免 slot（见缺陷 B） |
| B6 | visitIndex | 访问计数递增，支撑情报递进 |
| B7 | 选择链匹配 | all / any / ordered_all / count 四种语义 |
| B8 | **仲裁排序** | group 互斥、priority 降序、once 去重 |
| B9 | 口癖与占位 | 警探「嗯」由渲染层前置；text 留白时回退 summary |
| B10 | effects 应用 | flag/item/relationship/npcState/行动配额全部生效 |
| B11 | 存档往返 | ToJson → FromJson 后状态完全一致 |

### 4.3 第 3 层 · 流程模拟（C + D 组）

| 编号 | 检查项 | 验证内容 |
|---|---|---|
| C1 | **死锁检测** | 任意状态下每个节点都有可用选项或 skipLine 出路 |
| C2 | 七日完整性 | 每次模拟都能从 D1 跑到 D7 |
| C3 | 结局可达性 | 随机策略下各结局的触达分布 |
| C4 | 场景静默 | 是否存在"进入场景后无任何可播内容" |
| D | **定向结局** | 为每个结局构造可达路径，验证条件确实能凑齐 |

三种模拟策略轮流跑，覆盖面互补：

| 策略 | 行为 | 主要用途 |
|---|---|---|
| `Full` | 跑该天全部节点 | 最大覆盖，找隐藏冲突 |
| `Random` | 随机跳过 35% 非强制节点 | 模拟真实玩家的不完整探索 |
| `Minimal` | 只跑强制节点 | **最差情况**，验证证据全无时不卡死 |

### 4.4 第 4 层 · PlayMode 手工验证

自动测试测不出"体验对不对"。这一层必须人工做。

| 编号 | 验证项 | 怎么做 | 通过标准 |
|---|---|---|---|
| E1 | D1 完整流程 | 从码头开局玩到 F1 酒馆 | 序章→搜证→报社→夜间→酒馆，台词连贯无断裂 |
| E2 | **多选节点** | D1 搜证「选2处」 | 能连续选 2 次，已选项消失，选满后播收束台词 |
| E3 | 强制节点 | D1 傍晚进酒馆 | F1 自动触发，priority 100 压过其他台词 |
| E4 | 条件变体 | 存档读档制造两种状态，各进一次警局 | 警探对"有无短函"给出不同台词 |
| E5 | 关系值分支 | D1 对警探友善/莽撞各玩一次 | trust≥2 邀你去警局，<2 只警告别乱写 |
| E6 | 物证查看 | 背包点开妻子的信 | 以 `handwritten_letter` 样式渲染，非对话框 |
| E7 | 口癖渲染 | 听警探说话 | 每句自动前置「嗯」，且 text 字段里没有硬写 |
| E8 | 兜底台词 | 反复进入同一场景刷完所有台词 | NPC 始终有话可说，不会沉默 |
| E9 | **法庭死锁防护** | 前六天不拿任何证据，直接进 D7 法庭 | 四选项全灰时走 skipLine，以"举证不足"收尾，不卡死 |
| E10 | 结局分支 | 分别打出 END_CANON / END_BAIL / END_LOSS | 三个结局都能正常播放并结算 |
| E11 | 存档读档 | 中途存档→退出→读档 | 日数、flag、道具、关系值、选择链全部还原 |
| E12 | 行动配额 | 一天内做满 3 次行动 | 配额耗尽后无法再消耗行动的选项 |

---

## 五、已发现并修复的缺陷

这几处是实测跑出来的，不是理论推演。**建议保留对应测试项作为回归防线。**

### 缺陷 A：4 条强制节点台词的 event 为 null（数据缺陷）

**现象**：`TriggerForcedNode(eventId)` 按 `line.Event == eventId` 匹配，而这 4 条台词的 `event` 与 `trigger.eventId` 全是 null，永远命中不了。

**影响**：其中最严重的 `DL_MGR_POLICE_D6_010`（经理认罪供词）是 `main.manager_confessed` 的**唯一置位来源**，而该 flag 是 END_CANON、羁押判决、律师防线崩塌的共同前置——**正典结局完全不可达**。

| ID | 日/地点 | 影响 |
|---|---|---|
| `DL_MGR_POLICE_D6_010` | D6 警局 | 置位 `main.veil_woman` + `main.manager_confessed` ← 致命 |
| `DL_MGR_POLICE_D6_011` | D6 警局 | 交代八十镑报酬 |
| `DL_LAW_COURT_D7_033` | D7 法庭 | 保释陈述 |
| `DL_LAW_CORRIDOR_D7_030` | D7 走廊 | 走廊质问回应 |

**修复**：新增 `TriggerForcedNodeAtScene(location)`，按「当前地点 + 当前日」兜底触发 forced_node 台词；`PlayScene` 内部在播进场台词前先调它。

**根治建议**：补全这 4 条的 `event` 字段（D6 警局建议 `D6_action_return`，D7 法庭建议 `F3_court`），然后 A12 就会转 PASS。

### 缺陷 B：时间窗口过滤的严格程度需分场景（引擎缺陷，我引入后自查发现）

**现象**：最初给 `EvaluateAndSort` 加了统一的 `time.slot` 过滤，结果实测发现 6 处台词会被误杀：

```
CN_D7_POLICE_MANAGER(slot=morning) → DL_DET_POLICE_D7_061 声明 slot=[afternoon]
CN_D7_NEWS_FINAL(slot=midnight)    → DL_EDT_NEWS_D7_031/032/033 声明 slot=[night]
CN_D7_POLICE_MANAGER.promptLine    → DL_DET_POLICE_D7_060 声明 slot=[afternoon]
```

`DL_DET_POLICE_D7_060` 置位 `main.twelve_letters`（定罪铁证），被过滤掉就会连带影响结局。

**根因**：把两类语义完全不同的触发路径混为一谈了。

**修复**：`MatchesTimeWindow` 按触发类型分严格程度——

| 触发类型 | day | slot | weather | visitIndex |
|---|---|---|---|---|
| `enter_scene` / `npc_initiative` / `idle_fallback`（场景扫描） | 严格 | **严格** | 严格 | 严格 |
| `forced_node` / `item_inspect` / `choice_response` / `ending`（显式触发） | 严格 | **豁免** | 豁免 | 豁免 |

场景扫描类是"玩家刚好在这个时空进到这个场景"才该说的话，酒馆 D1 dusk 的情报不该在 D2 morning 播出，必须严格。显式触发类由剧情节点或玩家操作直接点名，播出时机已由调用方保证，再卡 slot 就是自找麻烦。

对应测试项 B5。

### 缺陷 C：多选节点只选一次就结束（引擎缺陷）

**现象**：`PlayChoiceCoroutineInternal` 原先是单次选择结构，`maxPicks=2` 的节点选一次就播 `onExhausted` 收束了。

**影响**：4 个多选节点行为错误，其中 D1 搜证「选2处」是剧本明写的核心机制：

| 节点 | 剧本对应 |
|---|---|
| `CN_D1_WHARF_EVIDENCE` | 第一天 行动①「搜证选项（选2处）」 |
| `CN_D3_MGOFF_SEARCH` | 第三天 经理办公室搜查 |
| `CN_D5_TUNNEL` | 第五天 冷藏库与隧道 |
| `CN_D7_COURT_EVIDENCE` | 第七天 法庭举证 |

**修复**：改为选择循环，`picksUsed < maxPicks` 时持续呈现选项；`consumable` 选项选过后从列表中移除，避免重复选同一项；列表空了提前退出并走 skipLine 兜底。

对应测试项 E2。

### 缺陷 D：模拟器未评估 npc_initiative（测试缺陷，设计文档 §10.2 已记录）

**现象**：原模拟器只建模 `forced_node / enter_scene / item_inspect / ending` 四类触发，没实现 `npc_initiative` 与 group 互斥仲裁。

**影响**：`main.lawyer_bailed` 在模拟中始终不置位——法官的两条判决台词属 `judge_verdict` 互斥组、靠 `npc_initiative` 触发，模拟器根本没去求值它们。结果是**结局分布严重失真**，无法验证正典结局可达性。

**修复**：模拟循环在每个决策节点跑完后补一轮 `npc_initiative` 评估，走完整仲裁链（条件求值 → group 互斥 → priority 排序）。

**顺序很关键**：必须放在决策节点之后，因为法官判决依赖举证节点产生的 `main.wife_letter` / `main.manager_confessed`。

### 缺陷 E：END_WIFE_TESTIFY 永久被遮蔽（数据设计缺陷，A15 抓出）

**现象**：穷举全部 flag 组合后，`END_WIFE_TESTIFY` 在**任何状态下都无法胜出**。

**根因**：`CheckEnding` 按数组顺序取第一个命中项，而：

```
[0] END_CANON         requires: diamond_recovered + lawyer_convicted
[1] END_BAIL          requires: diamond_recovered + lawyer_bailed
[2] END_LOSS          forbids:  diamond_recovered
[3] END_WIFE_TESTIFY  requires: wife_testified + diamond_recovered   ← 排在最后
```

`judge_verdict` 组含恒真兜底 `DL_JDG_COURT_D7_003`，**判决 flag 必然置位**（`lawyer_convicted` 或 `lawyer_bailed` 二选一）。于是只要 `diamond_recovered=true`，END_CANON 或 END_BAIL 必定先命中，排在末位的 END_WIFE_TESTIFY 永远轮不到。

实测 300 次模拟中，即便 `hidden.wife_testified` 已置位，结局仍报 END_CANON。

**两种修法，取决于设计意图**：

- **若"妻子出庭"应是独立结局** → 把 `END_WIFE_TESTIFY` 前移到数组第 0 位，让它在满足时优先胜出
- **若它只是正典结局的一个变体** → 保持现状，但应从 `endings` 中移除，改为在 END_CANON 的 `lines` 里按 `hidden.wife_testified` 条件插入 `DL_WIF_COURT_D7_020`（该台词已存在）

> 注意 `END_LOSS` **不构成**遮蔽：它 `forbidsFlags: diamond_recovered`，而 END_WIFE_TESTIFY 恰恰 requires 它，两者互斥。A15 的归因逻辑已正确处理这一点。

对应测试项 A15、A16。

---

## 六、PlayMode 手工验证环境

第 4 层测试需要一个最小可玩的 UI。项目已交付一个 OnGUI 调试面板，挂上就能用，省去搭正式 UI 的时间。

### 6.1 最简 UI 实现

`Assets/Scripts/Dialogue/DebugDialogueUI.cs` 已交付，实现 `DialogueRunner.IDialogueUI`，
用 `OnGUI` 直接绘制，**不需要搭 Canvas**。挂到与 `DialogueRunner` 同一个 GameObject 上，
`Start()` 会自动完成 `runner.Init(this)`（可用 Inspector 的 `Auto Init` 关闭）。

它画三样东西：

| 区域 | 内容 | 用途 |
|---|---|---|
| 顶部状态栏 | `D{n} {slot} \| 行动剩余 \| 地点 \| flag数 \| 道具数` | 随时确认当前状态，验 E12 行动配额 |
| 底部对话框 | NPC 名 + 台词正文 + 「继续 ▶」按钮 | 验台词流程；NPC 名后会附 `[render样式]` 标注，验 E6 文书渲染 |
| 中部选项区 | a/b/c/d 选项，条件不足者置灰并标注「（条件不足）」 | 验 E2 多选、E9 死锁置灰 |

对话框正文区可滚动，长文书不会溢出。

> `[handwritten_letter]` / `[typed_document]` 这类标注是刻意加的：
> 它让你在测试期就能确认 `delivery.render` 被正确读出，
> 正式 UI 据此切换渲染样式（信件、打字机文档、报纸等），不必等到接美术资源才发现字段没生效。

若你要接正式 UI（UGUI / Canvas），只需另写一个类实现同一个 `IDialogueUI` 接口：

```csharp
public class MyDialogueUI : MonoBehaviour, DialogueRunner.IDialogueUI
{
    public void ShowLine(DialogueLine line, string displayText)      { /* 填充对话框 */ }
    public void ShowNarration(DialogueLine line, string displayText) { /* 按 line.Delivery.Render 切换样式 */ }
    public void ShowChoice(ChoiceNode node,
        List<DialogueRunner.ChoiceOptionUI> options, Action<string> onPick) { /* 生成选项按钮，点击回调 onPick(id) */ }
    public void Hide()                                               { /* 关闭全部面板 */ }
    public IEnumerator WaitForContinue()                             { /* yield 到玩家点击继续 */ }
}
```

`DialogueRunner` 与 UI 层完全解耦——换 UI 不需要动引擎一行代码。

### 6.2 测试场景搭建

1. 新建场景 `TestDialogue`
2. 创建空 GameObject，命名 `DialogueSystem`
3. 挂 `DialogueRunner` + `DebugDialogueUI` 两个组件
4. `DialogueRunner` 的 Json Path 填 `dialogue/dialogue_bank.json`
5. 把 `台词库.json` 复制到 `Assets/StreamingAssets/dialogue/dialogue_bank.json`
6. Play

### 6.3 测试驱动台

已交付 `TestDriver.cs`，挂到与 `DialogueRunner` 同一个 GameObject 上即可。
PlayMode 下右上角会显示按键面板（F12 可隐藏），无需记忆。

按键映射：

| 按键 | 功能 | 对应测试项 |
|---|---|---|
| `1` ~ `7` | 跳到第 N 天（slot 重置为 morning，天气按剧本自动设置） | — |
| `F1` | 触发强制节点 F1（自动设 D1 dusk） | E3 |
| `F2` | 触发强制节点 F2（自动设 D4 afternoon） | E3 |
| `F3` | 触发强制节点 F3（自动设 D7 morning） | E3 |
| `Q` | 进入 12号码头（morning） | E1 |
| `W` | 进入 破锚酒馆（dusk） | E3 |
| `E` | 进入 贵族宅邸（afternoon） | E3 |
| `R` | 进入 码头警局（morning） | E4 |
| `T` | 进入 法庭（morning） | E9 / E10 |
| `Y` | 进入 天鹅与皇冠旅馆（morning） | E6 |
| `U` | 进入 地下隧道（night） | — |
| `I` | 进入 皇后公园站（morning） | E10 |
| `Z` | 触发 `CN_D1_WHARF_EVIDENCE`（D1 搜证，maxPicks=2） | **E2 多选节点** |
| `X` | 触发 `CN_D7_COURT_EVIDENCE`（D7 法庭举证） | **E9 死锁防护** |
| `G` | 注入正典结局所需的 5 个 flag | E10 |
| `H` | 结局判定（打印命中的结局 ID） | E10 |
| `J` | 导出当前 GameState JSON 到 Console | E11 |
| `K` | 跳过当前台词（等同 `runner.Skip()`） | — |

> `G` 注入的 flag 可在 Inspector 的 `Canon Flags` 数组里改，
> 便于构造 END_BAIL / END_LOSS 等其它结局的状态（测试项 E10）。

**E9 法庭死锁防护测试步骤**（最容易出错，务必单独验）：

1. Play → 按 `7` 跳到 D7（**不要**按 G 注入 flag，保持证据全无）
2. 按 `X` 触发 `CN_D7_COURT_EVIDENCE`
3. 预期：四个选项全部显示为「条件不足」置灰，随后自动走 skipLine 播出警探的"证据不足"台词 `DL_DET_COURT_D7_052`，流程正常收尾
4. 若卡住不动或报错，说明 skipLine 兜底链路有问题

**E2 多选节点测试步骤**：

1. 按 `1` 跳到 D1 → 按 `Z`
2. 预期：出现 4 个搜证选项，选中一个后该项**从列表消失**，继续呈现剩余选项
3. 选满 2 次后自动播收束台词 `DL_DET_WHARF_D1_009`，不再要求第三次选择
4. 若选一次就直接收束，说明多选循环（缺陷 C）未生效

---

## 七、CI 集成建议

设计文档第十节建议把校验固化成 CI，这是对的。每次改数据都手点一遍编辑器窗口不现实。

`DialogueSelfTest` 是**纯逻辑类**（不依赖 MonoBehaviour 与 UnityEditor），已用 NUnit 薄封装为
`Assets/Editor/DialogueTests/DialogueBankTests.cs`，包含 13 个测试：

| 测试方法 | 覆盖 | 说明 |
|---|---|---|
| `A_静态数据校验_无失败项` | A 组全部 16 项 | **当前会红**，因缺陷 E（见下方注记） |
| `A_静态校验_已知缺陷清单` | — | `[Explicit]`，手动跑以打印当前欠账 |
| `B_引擎逻辑_全部通过` | B 组全部 11 项 | |
| `B5_显式触发豁免slot过滤` | 缺陷 B 回归防线 | 断言 `DL_DET_POLICE_D7_060` 在 morning 仍可播 |
| `B5_场景扫描严格匹配slot` | 缺陷 B 反向防线 | 断言 dusk 台词不在 morning 播出 |
| `B2_hidden命名空间不可逆` | 暗线保护 | 断言 `hidden.*` 拒绝清除 |
| `C_流程模拟_无死锁且七日可走完` | C 组，100 次 | |
| `C_最差情况_证据全无不卡死` | 测试项 E9 | 断言法庭节点 skipLine 指向 `DL_DET_COURT_D7_052` |
| `D_正典结局_条件可判定` | D 组 | |
| `D_正典结局_手动构造路径可达` | END_CANON 全链路 | 置 flag → 求值 judge_verdict → 断言命中 END_CANON |
| `D_缺陷E_支线结局当前被遮蔽` | 缺陷 E 记录 | 断言**当前**被 END_BAIL 抢占；修好后此测试会红 |

运行方式：

```
Window → General → Test Runner → EditMode → Run All
```

CI 命令行：

```
Unity.exe -batchmode -projectPath <项目路径> -runTests -testPlatform EditMode -testResults results.xml
```

> **关于 `A_静态数据校验_无失败项` 当前会红**：
> 缺陷 E（END_WIFE_TESTIFY 被遮蔽）会让 A15 报 FAIL。这是**真实的待修数据问题**，
> 不是测试写错了——修数据之前该测试会一直红，这正是它的作用。
>
> 若暂时不想阻塞 CI，两种处理方式：
> 1. 先修数据（把 `END_WIFE_TESTIFY` 前移到 `endings` 数组首位），测试即转绿 ← **推荐**
> 2. 临时把断言改为 `Assert.AreEqual(1, report.Failed)` 并在注释里写明欠账与修复期限
>
> 不要直接删掉这个测试——它是唯一能自动发现"结局永远不可达"这类逻辑死路的防线。

---

## 八、实测结果记录

以下为本次交付前用 Python 镜像引擎跑出的真实数据，可作为你首次运行 Unity 测试时的对照基线。

### 静态校验（A 组）

```
规模: 台词228 节点30 选项120 结局4
PASS  A2  ID唯一性
PASS  A3  ID格式
PASS  A4  枚举引用完整性
PASS  A5  悬空引用
PASS  A6  死条件（被引用的 flag 全部有置位来源）
INFO  A7  已置位但未被引用的 flag: 23 个（文案预留钩子）
WARN  A8  兜底覆盖: 20 条 idle_fallback，16 处 NPC×场景缺口
PASS  A9  节点出路
INFO  A10 互斥组 46 个，其中 20 个无恒真成员
PASS  A11 强制节点 F1/F2/F3
WARN  A12 event 为空的 forced_node: 4 条
WARN  A13 时段声明不一致: 6 处
FAIL  A15 END_WIFE_TESTIFY 永久被 END_BAIL 遮蔽
```

### 流程模拟（C 组，300 次，三策略轮转）

```
结局分布:
  full  END_LOSS    78     min   END_LOSS   100     rnd  END_LOSS   91
  full  END_BAIL    20                          rnd  END_BAIL     8
  full  END_CANON    2                          rnd  END_CANON     1

C1 死锁:      PASS  无死锁（三种策略均无）
C2 无结局:    0/300
C3 END_CANON: PASS  可达
C4 场景静默:  4 处 → customs_house(D3 morning) / newsroom(D5 afternoon)
                     tunnel(D5 night) / park_station(D6 morning)
平均播出台词: 75.5 条/局（min 35, max 111）
```

### 定向通关（D 组）

构造路径后确认 **END_CANON 达成**，关键 flag 全部置位：

```
结局: END_CANON — 结案·钻石归还、律师被羁押、经理认罪

main.anon_note          YES      main.diamond_recovered   YES
main.wife_letter        YES      main.lawyer_convicted    YES
main.manager_confessed  YES      main.lawyer_bailed       no
main.veil_woman         YES      main.twelve_letters      YES
hidden.wife_testified   YES  ← 已置位却仍报 END_CANON，印证缺陷 E
```

关键路径（最小充分集）：

```
D3 CN_D3_MGOFF_SEARCH.c        取走办公室钥匙 → side.cold_storage_key
D4 CN_D4_HOTEL_WIFE.c          承诺保护并请她出庭 → hidden.wife_testified
D5 CN_D5_HOTEL_LETTER.b        取走妻子的信 → main.wife_letter
D5 CN_D5_TUNNEL.b              找到隧道 → main.empty_box
D6 CN_D6_STATION_LOCKER.a      打开17号柜 → main.diamond_recovered
D6 CN_D6_POLICE_MANAGER.d      经理认罪链 → main.manager_confessed
D7 CN_D7_COURT_EVIDENCE.a + d  举证妻子的信 + 经理供词 → 羁押判决
```

> END_CANON 随机命中率仅约 2%（full 策略），因为依赖 D5、D6 两处 1/4 概率的正确选择。
> 这不是缺陷，是"乱选拿不到圆满结局"的设计意图；定向测试确认了路径确实存在。

---

## 九、待办清单

按优先级排序：

| 优先级 | 事项 | 影响 |
|---|---|---|
| **高** | 修复 END_WIFE_TESTIFY 遮蔽（前移数组位置，或改为 END_CANON 的条件变体） | 该结局当前完全无法达成 |
| **高** | 补全 4 条 forced_node 台词的 `event` 字段 | 目前靠场景兜底触发，语义不清 |
| 中 | 补齐 16 处 NPC×场景的 idle_fallback 缺口 | 玩家可能遇到沉默的 NPC |
| 中 | 修正 6 处台词 slot 与节点 slot 不一致 | 数据卫生，避免误导文案 |
| 低 | 处理 4 处场景静默（customs_house D3 / newsroom D5 / tunnel D5 / park_station D6） | 这几个场景缺兜底内容 |
| 低 | 决定 23 个未引用 flag 的去留 | 留着扩展变体，或精简 |
