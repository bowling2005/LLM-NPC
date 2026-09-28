#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Dialogue.Editor
{
    /// <summary>
    /// 台词库测试运行器（编辑器窗口）。
    /// 菜单：Tools → 台词库测试
    ///
    /// 不依赖 Unity Test Framework 的安装与配置，点按钮即可跑全部测试。
    /// </summary>
    public class DialogueTestWindow : EditorWindow
    {
        private DialogueSelfTest.TestReport _report;
        private Vector2 _scroll;
        private string _jsonPath = "";
        private int _simRuns = 200;
        private int _seed = 20260924;
        private bool _running;
        private string _statusMessage = "";

        private bool _runA = true, _runB = true, _runC = true, _runD = true;

        private static readonly Dictionary<string, bool> _foldout = new();

        [MenuItem("Tools/台词库测试", false, 101)]
        public static void ShowWindow()
        {
            var w = GetWindow<DialogueTestWindow>();
            w.titleContent = new GUIContent("台词库测试");
            w.minSize = new Vector2(820, 560);
            w.Show();
        }

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_jsonPath))
                _jsonPath = GuessJsonPath();
        }

        private static string GuessJsonPath()
        {
            string[] candidates =
            {
                Path.Combine(Application.streamingAssetsPath, "dialogue/dialogue_bank.json"),
                Path.Combine(Application.streamingAssetsPath, "dialogue/台词库.json"),
                Path.Combine(Application.dataPath, "../台词库.json"),
                Path.Combine(Application.dataPath, "../台词库/台词库.json")
            };

            foreach (var p in candidates)
                if (File.Exists(p)) return p;

            return candidates[0];
        }

        private void OnGUI()
        {
            DrawConfig();
            DrawActions();

            if (_report == null)
            {
                EditorGUILayout.HelpBox("点击「运行全部测试」开始。首次运行会加载台词库 JSON。", MessageType.Info);
                return;
            }

            DrawSummary();
            DrawResults();
        }

        // ═══════════════════════════════════════════════════════
        //  配置区
        // ═══════════════════════════════════════════════════════

        private void DrawConfig()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("测试配置", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            _jsonPath = EditorGUILayout.TextField("台词库 JSON", _jsonPath);

            if (GUILayout.Button("浏览", GUILayout.Width(50)))
            {
                string p = EditorUtility.OpenFilePanel("选择台词库 JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(p)) _jsonPath = p;
            }

            if (GUILayout.Button("定位", GUILayout.Width(50)))
            {
                _jsonPath = GuessJsonPath();
                if (!File.Exists(_jsonPath))
                    EditorUtility.DisplayDialog("未找到", "常见路径下未找到台词库 JSON，请手动浏览选择。", "好");
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            _simRuns = EditorGUILayout.IntField("模拟次数", _simRuns);
            _seed = EditorGUILayout.IntField("随机种子", _seed);

            GUILayout.FlexibleSpace();

            _runA = GUILayout.Toggle(_runA, " A·静态校验", EditorStyles.toolbarButton);
            _runB = GUILayout.Toggle(_runB, " B·引擎逻辑", EditorStyles.toolbarButton);
            _runC = GUILayout.Toggle(_runC, " C·流程模拟", EditorStyles.toolbarButton);
            _runD = GUILayout.Toggle(_runD, " D·定向结局", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ═══════════════════════════════════════════════════════
        //  操作区
        // ═══════════════════════════════════════════════════════

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();

            var old = GUI.enabled;
            GUI.enabled = !_running;

            if (GUILayout.Button("运行全部测试", GUILayout.Height(28)))
                RunAll();

            if (GUILayout.Button("仅静态校验", GUILayout.Height(28)))
                RunGroup(DialogueSelfTest.RunStaticValidation);

            if (GUILayout.Button("仅引擎逻辑", GUILayout.Height(28)))
                RunGroup(DialogueSelfTest.RunLogicTests);

            if (GUILayout.Button("仅流程模拟", GUILayout.Height(28)))
                RunGroup(() => DialogueSelfTest.RunPlaythroughSimulation(_simRuns, _seed));

            if (GUILayout.Button("仅定向结局", GUILayout.Height(28)))
                RunGroup(DialogueSelfTest.RunTargetedEndings);

            GUI.enabled = old && _report != null;

            if (GUILayout.Button("导出报告", GUILayout.Height(28)))
                ExportReport();

            GUI.enabled = old;

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_statusMessage))
                EditorGUILayout.LabelField(_statusMessage, EditorStyles.miniLabel);
        }

        private void RunAll()
        {
            if (!EnsureLoaded()) return;

            _running = true;
            try
            {
                _report = new DialogueSelfTest.TestReport();

                if (_runA) _report.Merge(DialogueSelfTest.RunStaticValidation());
                if (_runB) _report.Merge(DialogueSelfTest.RunLogicTests());
                if (_runC) _report.Merge(DialogueSelfTest.RunPlaythroughSimulation(_simRuns, _seed));
                if (_runD) _report.Merge(DialogueSelfTest.RunTargetedEndings());

                _statusMessage = $"完成于 {DateTime.Now:HH:mm:ss}";
            }
            finally
            {
                _running = false;
                Repaint();
            }
        }

        private void RunGroup(Func<DialogueSelfTest.TestReport> action)
        {
            if (!EnsureLoaded()) return;

            _running = true;
            try
            {
                _report = action();
                _statusMessage = $"完成于 {DateTime.Now:HH:mm:ss}";
            }
            finally
            {
                _running = false;
                Repaint();
            }
        }

        private bool EnsureLoaded()
        {
            if (!File.Exists(_jsonPath))
            {
                EditorUtility.DisplayDialog("文件不存在", $"找不到台词库 JSON：\n{_jsonPath}", "好");
                return false;
            }

            try
            {
                DialogueLoader.LoadFromAbsolutePath(_jsonPath);
                return true;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("加载失败", e.Message, "好");
                return false;
            }
        }

        private void ExportReport()
        {
            if (_report == null) return;

            string defaultName = $"台词库测试报告_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string path = EditorUtility.SaveFilePanel("导出测试报告",
                Path.GetDirectoryName(_jsonPath), defaultName, "txt");

            if (string.IsNullOrEmpty(path)) return;

            File.WriteAllText(path, _report.ToText());
            EditorUtility.RevealInFinder(path);
            _statusMessage = $"报告已导出：{path}";
        }

        // ═══════════════════════════════════════════════════════
        //  结果展示
        // ═══════════════════════════════════════════════════════

        private void DrawSummary()
        {
            EditorGUILayout.BeginHorizontal("box");

            var old = GUI.color;

            EditorGUILayout.LabelField("测试结果", EditorStyles.boldLabel, GUILayout.Width(70));

            GUI.color = new Color(0.4f, 0.9f, 0.4f);
            EditorGUILayout.LabelField($"通过 {_report.Passed}", GUILayout.Width(70));

            GUI.color = new Color(1f, 0.85f, 0.3f);
            EditorGUILayout.LabelField($"警告 {_report.Warnings}", GUILayout.Width(70));

            GUI.color = _report.Failed > 0 ? new Color(1f, 0.45f, 0.45f) : Color.gray;
            EditorGUILayout.LabelField($"失败 {_report.Failed}", GUILayout.Width(70));

            GUI.color = old;

            GUILayout.FlexibleSpace();

            GUI.color = _report.AllPassed ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.45f, 0.45f);
            EditorGUILayout.LabelField(_report.AllPassed ? "全部通过" : "存在失败项",
                EditorStyles.boldLabel, GUILayout.Width(90));
            GUI.color = old;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawResults()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            string lastCategory = null;
            foreach (var res in _report.Results)
            {
                if (res.Category != lastCategory)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField(res.Category, EditorStyles.boldLabel);
                    lastCategory = res.Category;
                }

                DrawResultItem(res);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawResultItem(DialogueSelfTest.TestResult res)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();

            var old = GUI.color;
            GUI.color = res.Severity switch
            {
                DialogueSelfTest.Severity.Pass => new Color(0.45f, 0.85f, 0.45f),
                DialogueSelfTest.Severity.Warn => new Color(1f, 0.8f, 0.3f),
                _                              => new Color(1f, 0.4f, 0.4f)
            };

            string mark = res.Severity switch
            {
                DialogueSelfTest.Severity.Pass => "PASS",
                DialogueSelfTest.Severity.Warn => "WARN",
                _                              => "FAIL"
            };

            GUILayout.Label(mark, EditorStyles.miniBoldLabel, GUILayout.Width(42));
            GUI.color = old;

            bool hasDetails = res.Details != null && res.Details.Count > 0;
            bool fold = _foldout.GetValueOrDefault(res.Name);

            if (hasDetails)
                fold = EditorGUILayout.Foldout(fold, res.Name, true);
            else
                EditorGUILayout.LabelField(res.Name, EditorStyles.boldLabel);

            _foldout[res.Name] = fold;

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(res.Message))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(res.Message, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel--;
            }

            if (hasDetails && fold)
            {
                EditorGUI.indentLevel += 2;
                foreach (var d in res.Details)
                    EditorGUILayout.LabelField("· " + d, EditorStyles.wordWrappedMiniLabel);
                EditorGUI.indentLevel -= 2;
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
