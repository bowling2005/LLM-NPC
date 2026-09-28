using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dialogue
{
    /// <summary>
    /// 最简调试 UI：用 OnGUI 直接绘制，不需要搭 Canvas。
    /// 仅用于测试期验证台词流程；正式 UI 请另行实现 IDialogueUI。
    ///
    /// 用法：挂到与 DialogueRunner 同一个 GameObject 上，Start 会自动完成 Init。
    /// </summary>
    public class DebugDialogueUI : MonoBehaviour, DialogueRunner.IDialogueUI
    {
        private DialogueLine _current;
        private string _text = "";
        private DialogueRunner.ChoiceOptionUI[] _options;
        private Action<string> _onPick;

        private bool _waitingContinue;
        private bool _waitingChoice;

        private Vector2 _scroll;
        private GUIStyle _boxStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _textStyle;

        [Tooltip("是否在 Start 中自动初始化 DialogueRunner")]
        [SerializeField] private bool autoInit = true;

        void Start()
        {
            if (!autoInit) return;

            var runner = GetComponent<DialogueRunner>();
            if (runner == null) runner = gameObject.AddComponent<DialogueRunner>();

            runner.Init(this);
        }

        // ═══════════════════════════════════════════════════════
        //  IDialogueUI 实现
        // ═══════════════════════════════════════════════════════

        public void ShowLine(DialogueLine line, string displayText)
        {
            _current = line;
            _text = displayText ?? "";
            _waitingContinue = true;
            _waitingChoice = false;
        }

        public void ShowNarration(DialogueLine line, string displayText)
            => ShowLine(line, displayText);

        public void ShowChoice(ChoiceNode node, List<DialogueRunner.ChoiceOptionUI> options, Action<string> onPick)
        {
            _options = options?.ToArray();
            _onPick = onPick;
            _waitingChoice = true;
            _waitingContinue = false;
        }

        public void Hide()
        {
            _current = null;
            _text = "";
            _options = null;
            _waitingContinue = false;
            _waitingChoice = false;
        }

        public IEnumerator WaitForContinue()
        {
            while (_waitingContinue)
                yield return null;
        }

        // ═══════════════════════════════════════════════════════
        //  绘制
        // ═══════════════════════════════════════════════════════

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0.06f, 0.06f, 0.08f, 0.92f)) }
            };

            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.85f, 0.6f) }
            };

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        private static Texture2D MakeTex(int w, int h, Color c)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = c;

            var tex = new Texture2D(w, h) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            EnsureStyles();

            float w = Screen.width, h = Screen.height;

            DrawStatusBar(w);

            if (_current != null)
                DrawDialogueBox(w, h);

            if (_waitingChoice && _options != null)
                DrawOptions(w, h);
        }

        private void DrawStatusBar(float w)
        {
            if (!GameState.HasInstance) return;

            var s = GameState.Instance;
            GUI.Label(new Rect(10, 8, w - 20, 24),
                $"D{s.CurrentDay} {s.CurrentSlot} | 行动剩余 {s.ActionsLeft} | 地点 {s.CurrentLocation} | " +
                $"flag {s.GetAllFlags().Count} | 道具 {s.GetInventory().Count}");
        }

        private void DrawDialogueBox(float w, float h)
        {
            var box = new Rect(w * 0.08f, h * 0.60f, w * 0.84f, h * 0.33f);
            GUI.Box(box, "", _boxStyle);

            var npc = DialogueLoader.GetNpc(_current.Npc);
            string name = npc?.DisplayName ?? _current.Npc ?? "";

            // 文书/旁白标注渲染样式，便于验证 E6
            string render = _current.Delivery?.Render ?? "speech_bubble";
            if (render != "speech_bubble")
                name += $"   [{render}]";

            GUI.Label(new Rect(box.x + 14, box.y + 8, box.width - 28, 24), name, _nameStyle);

            var viewRect = new Rect(box.x + 14, box.y + 36, box.width - 28, box.height - 80);
            var content = new GUIContent(_text);
            float needed = _textStyle.CalcHeight(content, viewRect.width);
            _scroll = GUI.BeginScrollView(viewRect, _scroll, new Vector2(viewRect.width, Mathf.Max(needed, viewRect.height)));
            GUI.Label(new Rect(0, 0, viewRect.width - 20, needed), _text, _textStyle);
            GUI.EndScrollView();

            if (_waitingContinue)
            {
                if (GUI.Button(new Rect(box.xMax - 116, box.yMax - 38, 100, 28), "继续 ▶"))
                    _waitingContinue = false;
            }
        }

        private void DrawOptions(float w, float h)
        {
            float y = h * 0.26f;

            foreach (var o in _options)
            {
                bool old = GUI.enabled;
                GUI.enabled = o.Available;

                string label = $"{o.Id.ToUpper()}. {o.Label}";
                if (!o.Available) label += "   （条件不足）";

                if (GUI.Button(new Rect(w * 0.14f, y, w * 0.72f, 38), label))
                {
                    _waitingChoice = false;
                    _onPick?.Invoke(o.Id);
                }

                GUI.enabled = old;
                y += 46;
            }
        }
    }
}
