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
    /// Unity 编辑器台词填写工具。
    /// 供文案策划在 Unity 内直接浏览、筛选、填写台词 text 字段并保存回 JSON。
    ///
    /// 菜单路径：Tools → 台词编辑器
    /// </summary>
    public class DialogueEditorWindow : EditorWindow
    {
        // ═══════════════════════════════════════════════════════
        //  数据结构
        // ═══════════════════════════════════════════════════════

        private DialogueBank _bank;
        private string _jsonPath = "";

        // 筛选状态
        private string _searchText = "";
        private string _filterNpc = "全部";
        private string _filterLocation = "全部";
        private string _filterType = "全部";
        private int _filterDay = 0; // 0 = 全部
        private bool _showEmptyOnly = false;
        private bool _showFilledOnly = false;

        // 滚动位置
        private Vector2 _scrollPosition;
        private Vector2 _detailScroll;

        // 当前选中的台词
        private DialogueLine _selectedLine;
        private string _editingText = "";
        private bool _hasUnsavedChanges = false;

        // 列表缓存
        private List<DialogueLine> _filteredLines = new();
        private bool _needsRefresh = true;

        // NPC / Location 下拉选项
        private string[] _npcOptions;
        private string[] _locationOptions;
        private string[] _typeOptions = { "全部", "dialogue", "narration", "document", "inner" };

        // 统计
        private int _totalLines = 0;
        private int _filledLines = 0;
        private int _emptyLines = 0;

        // 布局
        private bool _showDetailPanel = true;
        private float _splitRatio = 0.5f;

        // ═══════════════════════════════════════════════════════
        //  窗口初始化
        // ═══════════════════════════════════════════════════════

        [MenuItem("Tools/台词编辑器", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<DialogueEditorWindow>();
            window.titleContent = new GUIContent("台词编辑器");
            window.minSize = new Vector2(1000, 600);
            window.Show();
        }

        private void OnEnable()
        {
            // 尝试自动加载
            TryAutoLoad();
        }

        private void TryAutoLoad()
        {
            // 默认路径
            string[] candidates = {
                Path.Combine(Application.streamingAssetsPath, "dialogue/dialogue_bank.json"),
                Path.Combine(Application.dataPath, "../台词库.json"),
                Path.Combine(Application.dataPath, "StreamingAssets/dialogue/dialogue_bank.json")
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    LoadFromFile(path);
                    return;
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        //  加载 / 保存
        // ═══════════════════════════════════════════════════════

        private void LoadFromFile(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                _bank = Newtonsoft.Json.JsonConvert.DeserializeObject<DialogueBank>(json);
                _jsonPath = path;

                BuildFilterOptions();
                RefreshFilteredList();
                UpdateStats();

                _selectedLine = null;
                _editingText = "";
                _hasUnsavedChanges = false;

                Repaint();
                Debug.Log($"[台词编辑器] 加载成功：{_bank.Lines.Count} 条台词");
            }
            catch (Exception e)
            {
                Debug.LogError($"[台词编辑器] 加载失败：{e.Message}");
            }
        }

        private void SaveToFile()
        {
            if (_bank == null || string.IsNullOrEmpty(_jsonPath))
                return;

            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(_bank, Newtonsoft.Json.Formatting.Indented,
                    new Newtonsoft.Json.JsonSerializerSettings
                    {
                        NullValueHandling = Newtonsoft.Json.NullValueHandling.Include
                    });

                File.WriteAllText(_jsonPath, json);
                _hasUnsavedChanges = false;
                UpdateStats();

                Debug.Log($"[台词编辑器] 已保存至：{_jsonPath}");
                ShowNotification(new GUIContent("保存成功"));
            }
            catch (Exception e)
            {
                Debug.LogError($"[台词编辑器] 保存失败：{e.Message}");
                ShowNotification(new GUIContent($"保存失败：{e.Message}"));
            }
        }

        // ═══════════════════════════════════════════════════════
        //  GUI
        // ═══════════════════════════════════════════════════════

        private void OnGUI()
        {
            if (_bank == null)
            {
                DrawLoadUI();
                return;
            }

            EditorGUILayout.BeginVertical();

            // 工具栏
            DrawToolbar();

            // 主内容区（左右分栏）
            EditorGUILayout.BeginHorizontal();

            // 左侧：筛选 + 列表
            float listWidth = position.width * (1f - _splitRatio);
            EditorGUILayout.BeginVertical(GUILayout.Width(listWidth));
            DrawFilterPanel();
            DrawLineList();
            EditorGUILayout.EndVertical();

            // 分割线
            _splitRatio = EditorGUILayout.Slider(_splitRatio, 0.3f, 0.7f, GUILayout.Width(4));

            // 右侧：详情编辑
            EditorGUILayout.BeginVertical();
            DrawDetailPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // 状态栏
            DrawStatusBar();

            EditorGUILayout.EndVertical();
        }

        // ─── 加载界面 ─────────────────────────────────────────

        private void DrawLoadUI()
        {
            GUILayout.Space(50);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(500));

            EditorGUILayout.LabelField("台词编辑器", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox("请先加载台词库 JSON 文件", MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            _jsonPath = EditorGUILayout.TextField("文件路径", _jsonPath);

            if (GUILayout.Button("浏览", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择台词库 JSON", Application.dataPath, "json");
                if (!string.IsNullOrEmpty(path))
                    _jsonPath = path;
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("加载", GUILayout.Height(30)))
            {
                if (File.Exists(_jsonPath))
                    LoadFromFile(_jsonPath);
                else
                    ShowNotification(new GUIContent("文件不存在"));
            }

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ─── 工具栏 ────────────────────────────────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // 保存按钮
            var color = _hasUnsavedChanges ? Color.yellow : Color.white;
            var oldColor = GUI.color;
            GUI.color = color;

            if (GUILayout.Button("保存 (Ctrl+S)", EditorStyles.toolbarButton))
                SaveToFile();

            GUI.color = oldColor;

            // 重新加载
            if (GUILayout.Button("重新加载", EditorStyles.toolbarButton))
            {
                if (File.Exists(_jsonPath))
                    LoadFromFile(_jsonPath);
            }

            GUILayout.FlexibleSpace();

            // 统计
            EditorGUILayout.LabelField(
                $"总计 {_totalLines} 条 | 已填写 {_filledLines} | 未填写 {_emptyLines}",
                EditorStyles.miniLabel);

            // 路径
            EditorGUILayout.LabelField(_jsonPath, EditorStyles.miniLabel, GUILayout.Width(300));

            EditorGUILayout.EndHorizontal();

            // Ctrl+S 快捷键
            if (Event.current.type == EventType.KeyDown &&
                Event.current.control && Event.current.keyCode == KeyCode.S)
            {
                SaveToFile();
                Event.current.Use();
            }
        }

        // ─── 筛选面板 ─────────────────────────────────────────

        private void DrawFilterPanel()
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("筛选", EditorStyles.boldLabel);

            // 搜索框
            EditorGUILayout.BeginHorizontal();
            _searchText = EditorGUILayout.TextField("搜索", _searchText);

            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                _searchText = "";
                _needsRefresh = true;
            }

            EditorGUILayout.EndHorizontal();

            // NPC 筛选
            if (_npcOptions != null && _npcOptions.Length > 0)
            {
                int npcIndex = Array.IndexOf(_npcOptions, _filterNpc);
                if (npcIndex < 0) npcIndex = 0;
                npcIndex = EditorGUILayout.Popup("NPC", npcIndex, _npcOptions);
                string newFilter = _npcOptions[npcIndex];

                if (newFilter != _filterNpc)
                {
                    _filterNpc = newFilter;
                    _needsRefresh = true;
                }
            }

            // 地点筛选
            if (_locationOptions != null && _locationOptions.Length > 0)
            {
                int locIndex = Array.IndexOf(_locationOptions, _filterLocation);
                if (locIndex < 0) locIndex = 0;
                locIndex = EditorGUILayout.Popup("地点", locIndex, _locationOptions);
                string newFilter = _locationOptions[locIndex];

                if (newFilter != _filterLocation)
                {
                    _filterLocation = newFilter;
                    _needsRefresh = true;
                }
            }

            // 类型筛选
            int typeIndex = Array.IndexOf(_typeOptions, _filterType);
            if (typeIndex < 0) typeIndex = 0;
            typeIndex = EditorGUILayout.Popup("类型", typeIndex, _typeOptions);

            if (_typeOptions[typeIndex] != _filterType)
            {
                _filterType = _typeOptions[typeIndex];
                _needsRefresh = true;
            }

            // 日筛选
            string[] dayOptions = { "全部", "D1", "D2", "D3", "D4", "D5", "D6", "D7" };
            int newDay = EditorGUILayout.Popup("日", _filterDay, dayOptions);

            if (newDay != _filterDay)
            {
                _filterDay = newDay;
                _needsRefresh = true;
            }

            // 填写状态筛选
            EditorGUILayout.BeginHorizontal();

            bool newShowEmpty = EditorGUILayout.ToggleLeft("仅未填写", _showEmptyOnly);
            bool newShowFilled = EditorGUILayout.ToggleLeft("仅已填写", _showFilledOnly);

            if (newShowEmpty != _showEmptyOnly)
            {
                _showEmptyOnly = newShowEmpty;
                _showFilledOnly = false;
                _needsRefresh = true;
            }

            if (newShowFilled != _showFilledOnly)
            {
                _showFilledOnly = newShowFilled;
                _showEmptyOnly = false;
                _needsRefresh = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ─── 台词列表 ─────────────────────────────────────────

        private void DrawLineList()
        {
            if (_needsRefresh)
            {
                RefreshFilteredList();
                _needsRefresh = false;
            }

            EditorGUILayout.LabelField($"匹配 {_filteredLines.Count} 条", EditorStyles.miniLabel);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            foreach (var line in _filteredLines)
            {
                bool isSelected = _selectedLine != null && _selectedLine.Id == line.Id;

                EditorGUILayout.BeginHorizontal(isSelected ? "Selection" : "box");

                // 填写状态指示
                bool hasText = !string.IsNullOrEmpty(line.Text);
                var oldColor = GUI.color;
                GUI.color = hasText ? Color.green : new Color(1f, 0.5f, 0.5f);
                GUILayout.Label("●", GUILayout.Width(15));
                GUI.color = oldColor;

                // ID
                GUILayout.Label(line.Id, EditorStyles.miniLabel, GUILayout.Width(200));

                // Summary
                GUILayout.Label(
                    string.IsNullOrEmpty(line.Summary) ? "[无简述]" : line.Summary,
                    EditorStyles.miniLabel,
                    GUILayout.ExpandWidth(true));

                // NPC
                var npc = _bank.Enums.Npc.Find(n => n.Id == line.Npc);
                string npcName = npc?.DisplayName ?? line.Npc ?? "?";
                GUILayout.Label(npcName, EditorStyles.miniLabel, GUILayout.Width(80));

                if (GUILayout.Button("编辑", GUILayout.Width(40)))
                {
                    SelectLine(line);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ─── 详情编辑面板 ──────────────────────────────────────

        private void DrawDetailPanel()
        {
            if (_selectedLine == null)
            {
                GUILayout.Space(50);
                EditorGUILayout.HelpBox("从左侧列表选择一条台词进行编辑", MessageType.Info);
                return;
            }

            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            EditorGUILayout.LabelField("台词编辑", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 基本信息（只读）
            EditorGUILayout.LabelField("ID", _selectedLine.Id);

            var npc = _bank.Enums.Npc.Find(n => n.Id == _selectedLine.Npc);
            string npcName = npc?.DisplayName ?? _selectedLine.Npc ?? "?";
            EditorGUILayout.LabelField("NPC", $"{npcName} ({_selectedLine.Npc})");

            if (npc?.VerbalTic != null)
                EditorGUILayout.LabelField("口癖", npc.VerbalTic);

            var loc = _bank.Enums.Location.Find(l => l.Id == _selectedLine.Location);
            EditorGUILayout.LabelField("地点", loc?.Name ?? _selectedLine.Location ?? "—");

            EditorGUILayout.LabelField("日/时段",
                $"D{_selectedLine.Time?.Day ?? 0} · {string.Join(", ", _selectedLine.Time?.Slot ?? new List<string>())}");

            EditorGUILayout.LabelField("类型", _selectedLine.Type);
            EditorGUILayout.LabelField("触发方式", _selectedLine.Trigger?.Type ?? "—");
            EditorGUILayout.LabelField("优先级", _selectedLine.Priority.ToString());
            EditorGUILayout.LabelField("仅一次", _selectedLine.Once ? "是" : "否");

            if (!string.IsNullOrEmpty(_selectedLine.Group))
                EditorGUILayout.LabelField("互斥组", _selectedLine.Group);

            EditorGUILayout.Space();

            // Summary（只读，参考用）
            EditorGUILayout.LabelField("内容简述（参考）", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_selectedLine.Summary ?? "[无]", EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space();

            // ── 台词正文编辑 ──
            EditorGUILayout.LabelField("台词正文", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(npc?.VerbalTic))
                EditorGUILayout.HelpBox(
                    $"口癖「{npc.VerbalTic}」由渲染层自动前置，请勿在正文中写入。",
                    MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _editingText = EditorGUILayout.TextArea(_editingText,
                GUILayout.Height(120));

            if (EditorGUI.EndChangeCheck())
                _hasUnsavedChanges = true;

            EditorGUILayout.Space();

            // 按钮
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("保存此条", GUILayout.Height(25)))
            {
                ApplyCurrentEdit();
            }

            if (GUILayout.Button("恢复原文", GUILayout.Height(25)))
            {
                _editingText = _selectedLine.Text ?? "";
            }

            if (GUILayout.Button("清空", GUILayout.Height(25)))
            {
                _editingText = "";
                _hasUnsavedChanges = true;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Delivery 信息（只读）
            if (_selectedLine.Delivery != null)
            {
                EditorGUILayout.LabelField("演出信息", EditorStyles.boldLabel);
                var d = _selectedLine.Delivery;
                EditorGUILayout.LabelField("情绪", d.Emotion ?? "—");
                EditorGUILayout.LabelField("语调", d.Tone ?? "—");
                EditorGUILayout.LabelField("动作", d.Action ?? "—");
                EditorGUILayout.LabelField("渲染", d.Render ?? "speech_bubble");
            }

            // Effects 信息（只读）
            if (_selectedLine.Effects != null)
            {
                EditorGUILayout.LabelField("效果", EditorStyles.boldLabel);
                var e = _selectedLine.Effects;

                if (e.SetFlags.Count > 0)
                    EditorGUILayout.LabelField("设置 Flag", string.Join(", ", e.SetFlags));

                if (e.AddItems.Count > 0)
                    EditorGUILayout.LabelField("获得道具", string.Join(", ", e.AddItems));

                if (e.Relationship != null)
                {
                    foreach (var kvp in e.Relationship)
                        EditorGUILayout.LabelField($"关系值 {kvp.Key}", $"{(kvp.Value > 0 ? "+" : "")}{kvp.Value}");
                }

                if (e.ConsumeAction)
                    EditorGUILayout.LabelField("消耗行动", "是");

                if (e.AdvanceDay)
                    EditorGUILayout.LabelField("推进日期", "是");
            }

            // Tags
            if (_selectedLine.Tags != null && _selectedLine.Tags.Count > 0)
            {
                EditorGUILayout.LabelField("标签", string.Join(", ", _selectedLine.Tags));
            }

            // Notes
            if (!string.IsNullOrEmpty(_selectedLine.Notes))
            {
                EditorGUILayout.LabelField("备注", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(_selectedLine.Notes, EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.EndScrollView();
        }

        // ─── 状态栏 ───────────────────────────────────────────

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (_hasUnsavedChanges)
                EditorGUILayout.LabelField("● 有未保存的修改", EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField("✓ 已保存", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (_selectedLine != null)
                EditorGUILayout.LabelField($"当前：{_selectedLine.Id}", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        // ═══════════════════════════════════════════════════════
        //  内部逻辑
        // ═══════════════════════════════════════════════════════

        private void BuildFilterOptions()
        {
            // NPC 选项
            var npcList = new List<string> { "全部" };
            npcList.AddRange(_bank.Enums.Npc.Select(n => n.DisplayName));
            _npcOptions = npcList.ToArray();

            // 地点选项
            var locList = new List<string> { "全部" };
            locList.AddRange(_bank.Enums.Location.Select(l => l.Name));
            _locationOptions = locList.ToArray();
        }

        private void RefreshFilteredList()
        {
            _filteredLines = _bank.Lines.Where(line =>
            {
                // 搜索
                if (!string.IsNullOrEmpty(_searchText))
                {
                    bool match = line.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
                              || (line.Summary?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                              || (line.Text?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false);

                    if (!match)
                        return false;
                }

                // NPC
                if (_filterNpc != "全部")
                {
                    var npc = _bank.Enums.Npc.Find(n => n.DisplayName == _filterNpc);
                    if (npc != null && line.Npc != npc.Id)
                        return false;
                }

                // 地点
                if (_filterLocation != "全部")
                {
                    var loc = _bank.Enums.Location.Find(l => l.Name == _filterLocation);
                    if (loc != null && line.Location != loc.Id)
                        return false;
                }

                // 类型
                if (_filterType != "全部" && line.Type != _filterType)
                    return false;

                // 日
                if (_filterDay > 0)
                {
                    int? day = line.Time?.Day;
                    if (day == null || day.Value != _filterDay)
                        return false;
                }

                // 填写状态
                if (_showEmptyOnly && !string.IsNullOrEmpty(line.Text))
                    return false;

                if (_showFilledOnly && string.IsNullOrEmpty(line.Text))
                    return false;

                return true;
            }).ToList();
        }

        private void UpdateStats()
        {
            if (_bank == null) return;

            _totalLines = _bank.Lines.Count;
            _filledLines = _bank.Lines.Count(l => !string.IsNullOrEmpty(l.Text));
            _emptyLines = _totalLines - _filledLines;
        }

        private void SelectLine(DialogueLine line)
        {
            // 如果有未保存的修改，先保存
            if (_hasUnsavedChanges && _selectedLine != null)
                ApplyCurrentEdit();

            _selectedLine = line;
            _editingText = line.Text ?? "";
            _hasUnsavedChanges = false;

            Repaint();
        }

        private void ApplyCurrentEdit()
        {
            if (_selectedLine == null)
                return;

            _selectedLine.Text = _editingText;
            _hasUnsavedChanges = false;

            UpdateStats();
            _needsRefresh = true;

            Debug.Log($"[台词编辑器] 已更新：{_selectedLine.Id}");
        }
    }
}
#endif
