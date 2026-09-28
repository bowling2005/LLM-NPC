using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// 测试驱动台：PlayMode 下用键盘直接跳转日期、场景与决策节点，
    /// 免去"为了测 D7 法庭而从头玩七天"的痛苦。
    ///
    /// 挂到与 DialogueRunner 同一个 GameObject 上。
    /// 按键映射见 OnGUI 中绘制的提示面板，或本文档末尾的清单。
    /// </summary>
    public class TestDriver : MonoBehaviour
    {
        private DialogueRunner _runner;
        private bool _showHelp = true;

        [Header("正典结局所需 flag（按 G 一键注入）")]
        [SerializeField] private string[] canonFlags =
        {
            "main.wife_letter",
            "main.manager_confessed",
            "main.diamond_recovered",
            "main.anon_note",
            "main.veil_woman"
        };

        void Start()
        {
            _runner = GetComponent<DialogueRunner>();
            if (_runner == null)
                Debug.LogError("[TestDriver] 同一 GameObject 上找不到 DialogueRunner，测试驱动台不可用");
        }

        void Update()
        {
            if (_runner == null || !GameState.HasInstance) return;

            var s = GameState.Instance;

            // ── 日期跳转：1~7 ──────────────────────────────
            for (int i = 1; i <= 7; i++)
            {
                if (Input.GetKeyDown(i.ToString()))
                {
                    s.SetDay(i);
                    Log($"跳到 D{i}（slot 重置为 morning，天气 {s.CurrentWeather}）");
                }
            }

            // ── 强制节点：F1 / F2 / F3 ─────────────────────
            if (Input.GetKeyDown(KeyCode.F1))
            {
                s.SetSlot("dusk");
                Log("触发 F1 酒馆揭示");
                _runner.PlayForcedNode("F1_tavern_reveal");
            }

            if (Input.GetKeyDown(KeyCode.F2))
            {
                s.SetDay(4); s.SetSlot("afternoon");
                Log("触发 F2 宅邸照片");
                _runner.PlayForcedNode("F2_mansion_photo");
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                s.SetDay(7); s.SetSlot("morning");
                Log("触发 F3 法庭");
                _runner.PlayForcedNode("F3_court");
            }

            // ── 场景跳转：Q W E R T Y U I ──────────────────
            if (Input.GetKeyDown(KeyCode.Q)) Enter("wharf_12", "morning");
            if (Input.GetKeyDown(KeyCode.W)) Enter("tavern", "dusk");
            if (Input.GetKeyDown(KeyCode.E)) Enter("mansion_study", "afternoon");
            if (Input.GetKeyDown(KeyCode.R)) Enter("police_station", "morning");
            if (Input.GetKeyDown(KeyCode.T)) Enter("court", "morning");
            if (Input.GetKeyDown(KeyCode.Y)) Enter("hotel_swan", "morning");
            if (Input.GetKeyDown(KeyCode.U)) Enter("tunnel", "night");
            if (Input.GetKeyDown(KeyCode.I)) Enter("park_station", "morning");

            // ── 决策节点：Z / X ────────────────────────────
            // Z = D1 搜证「选2处」，验证多选循环（测试项 E2）
            if (Input.GetKeyDown(KeyCode.Z))
            {
                s.SetDay(1); s.SetSlot("morning"); s.SetLocationWithoutVisit("wharf_12");
                Log("触发 CN_D1_WHARF_EVIDENCE（多选，maxPicks=2）");
                _runner.PlayChoice("CN_D1_WHARF_EVIDENCE");
            }

            // X = D7 法庭举证，验证死锁防护（测试项 E9）
            if (Input.GetKeyDown(KeyCode.X))
            {
                s.SetDay(7); s.SetSlot("morning"); s.SetLocationWithoutVisit("court");
                Log("触发 CN_D7_COURT_EVIDENCE。若未按 G 注入证据，四选项应全部置灰并走 skipLine 兜底");
                _runner.PlayChoice("CN_D7_COURT_EVIDENCE");
            }

            // ── 工具键 ─────────────────────────────────────
            // G：注入正典结局所需 flag
            if (Input.GetKeyDown(KeyCode.G))
            {
                foreach (var f in canonFlags) s.SetFlag(f);
                Log($"已注入 {canonFlags.Length} 个 flag：{string.Join(", ", canonFlags)}");
            }

            // H：结局判定
            if (Input.GetKeyDown(KeyCode.H))
            {
                var ending = DialogueManager.CheckEnding();
                Log(ending != null
                    ? $"结局命中：{ending.Id} — {ending.Name}（正典={ending.IsCanon}）"
                    : "当前无结局命中");
            }

            // J：导出当前状态
            if (Input.GetKeyDown(KeyCode.J))
                Log("当前 GameState：\n" + s.ToJson());

            // K：跳过当前台词
            if (Input.GetKeyDown(KeyCode.K))
                _runner.Skip();

            // F12：切换帮助面板
            if (Input.GetKeyDown(KeyCode.F12))
                _showHelp = !_showHelp;
        }

        private void Enter(string location, string slot)
        {
            var s = GameState.Instance;
            s.SetSlot(slot);
            Log($"进入 {location}（{slot}，第 {s.GetVisitCount(location) + 1} 次访问）");
            _runner.PlayScene(location);
        }

        private static void Log(string msg) => Debug.Log($"[TestDriver] {msg}");

        // ═══════════════════════════════════════════════════════
        //  帮助面板
        // ═══════════════════════════════════════════════════════

        void OnGUI()
        {
            if (!_showHelp) 
            {
                GUI.Label(new Rect(Screen.width - 150, 8, 140, 22), "F12 显示测试面板");
                return;
            }

            const string help =
                "测试驱动台                        (F12 隐藏)\n" +
                "────────────────────────────────────────\n" +
                "1~7   跳到第 N 天\n" +
                "F1/F2/F3  触发强制节点 F1/F2/F3\n" +
                "────────────────────────────────────────\n" +
                "Q 12号码头   W 破锚酒馆   E 贵族宅邸\n" +
                "R 码头警局   T 法庭       Y 旅馆\n" +
                "U 地下隧道   I 皇后公园站\n" +
                "────────────────────────────────────────\n" +
                "Z D1搜证(多选2项)  X D7法庭举证\n" +
                "────────────────────────────────────────\n" +
                "G 注入正典flag  H 结局判定\n" +
                "J 导出状态      K 跳过台词\n";

            var box = new Rect(Screen.width - 268, 34, 258, 268);
            GUI.Box(box, "");
            GUI.Label(new Rect(box.x + 10, box.y + 6, box.width - 20, box.height - 12), help);
        }
    }
}
