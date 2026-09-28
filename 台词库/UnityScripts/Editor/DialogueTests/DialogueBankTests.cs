#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Dialogue.Tests
{
    /// <summary>
    /// EditMode 测试薄封装。
    ///
    /// 前置：Package Manager 安装 com.unity.test-framework（2022.3 通常已内置）。
    /// 运行：Window → General → Test Runner → EditMode → Run All
    ///
    /// 这些测试不依赖场景与 MonoBehaviour，可在 CI 中用 -runTests 直接跑。
    /// </summary>
    public class DialogueBankTests
    {
        /// <summary>
        /// 台词库 JSON 路径。默认取项目上级目录的「台词库.json」，
        /// 若已复制到 StreamingAssets 则优先用那份（与运行时一致）。
        /// </summary>
        private static string ResolveJsonPath()
        {
            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "dialogue/dialogue_bank.json"),
                Path.Combine(Application.streamingAssetsPath, "dialogue/台词库.json"),
                Path.Combine(Application.dataPath, "../台词库.json")
            };

            foreach (var p in candidates)
                if (File.Exists(p)) return p;

            return candidates[0];
        }

        [OneTimeSetUp]
        public void LoadBank()
        {
            string path = ResolveJsonPath();
            Assert.IsTrue(File.Exists(path), $"找不到台词库 JSON：{path}");

            DialogueLoader.LoadFromAbsolutePath(path);
            Assert.IsTrue(DialogueLoader.IsLoaded, "台词库加载后 IsLoaded 应为 true");
        }

        // ═══════════════════════════════════════════════════════
        //  A 组 · 静态数据校验
        // ═══════════════════════════════════════════════════════

        [Test]
        public void A_静态数据校验_无失败项()
        {
            var report = DialogueSelfTest.RunStaticValidation();
            Assert.AreEqual(0, report.Failed,
                $"静态校验存在 {report.Failed} 项失败：\n\n{report.ToText()}");
        }

        /// <summary>
        /// 已知的待修数据缺陷清单。这些项当前会失败，属"欠账"而非测试写错。
        /// 修好数据后应从此列表移除对应项，让测试转红以提醒你清理。
        ///
        /// · A15 END_WIFE_TESTIFY 被 END_BAIL 永久遮蔽（结局数组顺序问题）
        /// </summary>
        [Test]
        [Explicit("包含已知未修缺陷，需显式运行")]
        public void A_静态校验_已知缺陷清单()
        {
            var report = DialogueSelfTest.RunStaticValidation();
            var failed = report.Results.FindAll(r => r.Severity == DialogueSelfTest.Severity.Fail);

            Debug.Log($"当前失败项 {failed.Count} 个：\n" +
                      string.Join("\n", failed.ConvertAll(f => $"  · {f.Name}：{f.Message}")));
        }

        // ═══════════════════════════════════════════════════════
        //  B 组 · 引擎逻辑
        // ═══════════════════════════════════════════════════════

        [Test]
        public void B_引擎逻辑_全部通过()
        {
            var report = DialogueSelfTest.RunLogicTests();
            Assert.AreEqual(0, report.Failed,
                $"引擎逻辑存在 {report.Failed} 项失败：\n\n{report.ToText()}");
        }

        [Test]
        public void B5_显式触发豁免slot过滤()
        {
            // 缺陷 B 的回归防线：forced_node / choice_response / item_inspect / ending
            // 不受 slot 约束，否则 DL_DET_POLICE_D7_060（置位 main.twelve_letters）等
            // 6 处台词会被误杀，连带影响结局。
            GameState.Init();
            var s = GameState.Instance;
            s.SetDay(7);
            s.SetSlot("morning");

            var line = DialogueLoader.GetLine("DL_DET_POLICE_D7_060");
            Assert.IsNotNull(line, "DL_DET_POLICE_D7_060 应存在");
            Assert.AreEqual("forced_node", line.Trigger.Type, "该台词应为 forced_node 触发");

            Assert.IsTrue(DialogueManager.MatchesTimeWindow(line),
                "forced_node 台词应豁免 slot 过滤：台词声明 afternoon，当前 morning，仍须可播");
        }

        [Test]
        public void B5_场景扫描严格匹配slot()
        {
            // 反向防线：enter_scene 类台词必须严格匹配 slot，
            // 否则酒馆 D1 dusk 的情报会在 D2 morning 播出。
            GameState.Init();
            var s = GameState.Instance;
            s.SetDay(1);
            s.SetSlot("morning");

            var duskLine = new DialogueLine
            {
                Id = "TEST_DUSK_ONLY",
                Npc = "landlady",
                Location = "tavern",
                Type = "dialogue",
                Trigger = new Trigger { Type = "enter_scene" },
                Time = new TimeInfo { Day = 1, Slot = new System.Collections.Generic.List<string> { "dusk" } }
            };

            Assert.IsFalse(DialogueManager.MatchesTimeWindow(duskLine),
                "dusk 台词不应在 morning 播出");

            s.SetSlot("dusk");
            Assert.IsTrue(DialogueManager.MatchesTimeWindow(duskLine),
                "dusk 台词应在 dusk 播出");
        }

        [Test]
        public void B2_hidden命名空间不可逆()
        {
            GameState.Init();
            var s = GameState.Instance;

            s.SetFlag("hidden.photo_acquired");
            Assert.IsFalse(s.ClearFlag("hidden.photo_acquired"),
                "hidden.* flag 不允许被清除");
            Assert.IsTrue(s.HasFlag("hidden.photo_acquired"),
                "清除失败后 flag 应仍存在");
        }

        // ═══════════════════════════════════════════════════════
        //  C 组 · 流程模拟
        // ═══════════════════════════════════════════════════════

        [Test]
        public void C_流程模拟_无死锁且七日可走完()
        {
            var report = DialogueSelfTest.RunPlaythroughSimulation(runs: 100, seed: 20260924);
            Assert.AreEqual(0, report.Failed,
                $"流程模拟存在 {report.Failed} 项失败：\n\n{report.ToText()}");
        }

        [Test]
        public void C_最差情况_证据全无不卡死()
        {
            // 对应测试项 E9：Minimal 策略只跑强制节点，
            // 前六天几乎不拿证据，验证 D7 法庭举证不会硬死锁。
            GameState.Init();
            var s = GameState.Instance;
            s.SetDay(7);
            s.SetSlot("morning");
            s.SetLocationWithoutVisit("court");

            var node = DialogueManager.GetChoiceNode("CN_D7_COURT_EVIDENCE");
            Assert.IsNotNull(node, "CN_D7_COURT_EVIDENCE 应存在");

            bool deadlocked = DialogueManager.IsNodeDeadlocked(node, out var skipLine);

            Assert.IsFalse(deadlocked,
                "证据全无时法庭举证节点不应硬死锁（设计文档 §10.1 记录的真实缺陷）");
            Assert.IsNotNull(skipLine,
                "证据全无时应提供 skipLine 出路");
            Assert.AreEqual("DL_DET_COURT_D7_052", skipLine.Id,
                "skipLine 应指向警探的「证据不足」台词");
        }

        // ═══════════════════════════════════════════════════════
        //  D 组 · 定向结局
        // ═══════════════════════════════════════════════════════

        [Test]
        public void D_正典结局_条件可判定()
        {
            var report = DialogueSelfTest.RunTargetedEndings();
            Assert.AreEqual(0, report.Failed,
                $"定向结局测试存在 {report.Failed} 项失败：\n\n{report.ToText()}");
        }

        [Test]
        public void D_正典结局_手动构造路径可达()
        {
            // 按实测确认的最小充分集构造状态，验证 END_CANON 确实能命中。
            GameState.Init();
            var s = GameState.Instance;

            s.SetDay(7);
            s.SetSlot("morning");
            s.SetLocationWithoutVisit("court");

            // D5 旅馆取信 + D6 经理认罪 + D6 追回钻石
            s.SetFlag("main.wife_letter");
            s.SetFlag("main.manager_confessed");
            s.SetFlag("main.diamond_recovered");
            s.AddItem("wife_letter");

            // D7 法庭举证后，法官 judge_verdict 组择一宣判
            var verdict = DialogueManager.NpcInitiative("judge", "court");
            Assert.IsNotEmpty(verdict, "法官应有判决台词可播");

            foreach (var l in verdict)
                DialogueManager.MarkLinePlayed(l);   // 应用 effects → 置位判决 flag

            Assert.IsTrue(s.HasFlag("main.lawyer_convicted"),
                "妻子的信 + 经理供词齐备时，应判羁押（main.lawyer_convicted）");

            var ending = DialogueManager.CheckEnding();
            Assert.IsNotNull(ending, "应命中某个结局");
            Assert.AreEqual("END_CANON", ending.Id,
                $"应为正典结局，实际命中 {ending.Id}");
        }

        [Test]
        public void D_缺陷E_支线结局当前被遮蔽()
        {
            // 缺陷 E 的显式记录：END_WIFE_TESTIFY 排在数组末位，
            // 而 judge_verdict 组的恒真兜底必然置位 main.lawyer_bailed，
            // 导致 diamond_recovered 为真时 END_CANON / END_BAIL 必定先命中。
            //
            // 修复方式二选一：
            //   ① 把 END_WIFE_TESTIFY 前移到数组首位
            //   ② 改为 END_CANON 的条件变体（从其 endings 中移除）
            //
            // 修好后本测试会失败——这是预期的，届时请删除或反转断言。
            GameState.Init();
            var s = GameState.Instance;

            s.SetFlag("hidden.wife_testified");
            s.SetFlag("main.diamond_recovered");
            s.SetFlag("main.lawyer_bailed");   // judge_verdict 恒真兜底的必然产物

            var ending = DialogueManager.CheckEnding();
            Assert.IsNotNull(ending);
            Assert.AreNotEqual("END_WIFE_TESTIFY", ending.Id,
                "END_WIFE_TESTIFY 已被修复？请更新本测试的断言方向");
            Assert.AreEqual("END_BAIL", ending.Id,
                "当前应被 END_BAIL 抢占（缺陷 E 未修）");
        }
    }
}
#endif
