using System.Collections.Generic;
using UnityEngine;

namespace WildguardModFramework.Notifications {
    /// <summary>
    /// IMGUI-based corner notification display. Placeholder — will be replaced with a
    /// proper UIToolkit overlay (closeable, animated) in a future WMF version.
    /// Lives on the persistent CoroutineRunner GameObject.
    /// </summary>
    internal sealed class ModNotificationOverlay : MonoBehaviour {
        private const float AutoCloseSeconds = 5f;
        private const float Width = 420f;
        private const float LineHeight = 26f;
        private const float Padding = 8f;

        private readonly struct Entry {
            public readonly string Message;
            public readonly NotificationLevel Level;
            public readonly bool AutoClose;
            public readonly float BornAt;

            public Entry(string message, NotificationLevel level, bool autoClose) {
                Message = message;
                Level = level;
                AutoClose = autoClose;
                BornAt = Time.realtimeSinceStartup;
            }
        }

        private readonly List<Entry> _entries = new();
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;

        internal void Show(string message, NotificationLevel level, bool autoClose) {
            _entries.Add(new Entry(message, level, autoClose));
        }

        private void OnGUI() {
            if (_entries.Count == 0) { return; }

            EnsureStyles();

            float now = Time.realtimeSinceStartup;
            for (int i = _entries.Count - 1; i >= 0; i--) {
                if (_entries[i].AutoClose && now - _entries[i].BornAt >= AutoCloseSeconds) {
                    _entries.RemoveAt(i);
                }
            }

            if (_entries.Count == 0) { return; }

            float rowH = LineHeight + Padding;
            float totalH = _entries.Count * rowH + Padding;
            float x = Screen.width - Width - 16f;
            float y = Screen.height - totalH - 16f;

            GUI.Box(new Rect(x - Padding, y - Padding, Width + Padding * 2f, totalH + Padding), GUIContent.none, _boxStyle);

            float cy = y;
            foreach (var entry in _entries) {
                _labelStyle.normal.textColor = LevelColor(entry.Level);
                GUI.Label(new Rect(x, cy, Width, LineHeight), entry.Message, _labelStyle);
                cy += rowH;
            }
        }

        private void EnsureStyles() {
            if (_labelStyle != null) { return; }

            _labelStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 15,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft,
                richText = false,
            };
            _labelStyle.normal.textColor = Color.white;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.55f));
        }

        private static Color LevelColor(NotificationLevel level) => level switch {
            NotificationLevel.Warn => new Color(1f, 0.85f, 0.1f),
            NotificationLevel.Error => new Color(1f, 0.35f, 0.35f),
            _ => new Color(0.9f, 0.9f, 0.9f),
        };

        private static Texture2D MakeTex(int w, int h, Color col) {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) { pix[i] = col; }
            var tex = new Texture2D(w, h);
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }
    }
}
