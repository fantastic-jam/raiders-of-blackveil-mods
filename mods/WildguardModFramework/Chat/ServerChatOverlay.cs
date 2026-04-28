using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WildguardModFramework.Chat {
    internal sealed class ServerChatOverlay {
        // _root covers the full page so it can intercept clicks while input is open.
        private readonly VisualElement _root;
        private readonly VisualElement _chatContent; // bottom-left 400px area
        private readonly ScrollView _logScroll;
        private readonly VisualElement _logPanel;
        private readonly VisualElement _inputRow;
        private readonly TextField _inputField;
        private IVisualElementScheduledItem _fadeTimer;

        internal event Action<string> OnSendRequested;

        internal ServerChatOverlay(VisualElement pageRoot) {
            // Full-screen root — normally passes clicks through; intercepts when input is open.
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.top = _root.style.right = _root.style.bottom = _root.style.left = 0;
            _root.pickingMode = PickingMode.Ignore;

            // Actual chat area pinned to bottom-left.
            _chatContent = new VisualElement();
            _chatContent.style.position = Position.Absolute;
            _chatContent.style.bottom = 0;
            _chatContent.style.left = 0;
            _chatContent.style.width = 400;
            _chatContent.pickingMode = PickingMode.Ignore;

            _logPanel = new VisualElement();
            _logPanel.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _logPanel.style.maxHeight = 240;
            _logPanel.style.borderTopLeftRadius = 4;
            _logPanel.style.borderTopRightRadius = 4;
            _logPanel.style.paddingTop = _logPanel.style.paddingBottom = 4;
            _logPanel.style.paddingLeft = _logPanel.style.paddingRight = 8;
            _logPanel.pickingMode = PickingMode.Ignore;

            _logScroll = new ScrollView(ScrollViewMode.Vertical);
            _logScroll.style.maxHeight = 232;
            _logScroll.pickingMode = PickingMode.Ignore;
            _logScroll.contentContainer.pickingMode = PickingMode.Ignore;
            _logPanel.Add(_logScroll);
            _chatContent.Add(_logPanel);

            _inputRow = new VisualElement();
            _inputRow.style.flexDirection = FlexDirection.Row;
            _inputRow.style.alignItems = Align.Center;
            _inputRow.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
            _inputRow.style.paddingLeft = _inputRow.style.paddingRight = 8;
            _inputRow.style.paddingTop = _inputRow.style.paddingBottom = 5;
            _inputRow.style.borderBottomLeftRadius = 4;
            _inputRow.style.borderBottomRightRadius = 4;
            _inputRow.pickingMode = PickingMode.Position;
            _inputRow.style.display = DisplayStyle.None;

            var prefix = new Label { text = ">" };
            prefix.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            prefix.style.fontSize = 20;
            prefix.style.marginRight = 4;
            prefix.pickingMode = PickingMode.Ignore;
            _inputRow.Add(prefix);

            _inputField = new TextField();
            _inputField.style.flexGrow = 1;
            _inputField.style.backgroundColor = Color.clear;
            _inputField.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            _inputField.style.fontSize = 20;
            _inputField.style.overflow = Overflow.Visible;
            _inputField.maxLength = 200;
            // Clear the inner input element's USS background; allow overflow so descenders are not clipped.
            _inputField.style.marginLeft = 0;
            _inputField.RegisterCallback<AttachToPanelEvent>(_ => {
                var inner = _inputField.Q(className: TextField.inputUssClassName);
                if (inner != null) {
                    inner.style.backgroundColor = Color.clear;
                    inner.style.paddingLeft = inner.style.paddingRight =
                        inner.style.paddingTop = inner.style.paddingBottom = 0;
                    inner.style.borderTopWidth = inner.style.borderRightWidth =
                        inner.style.borderBottomWidth = inner.style.borderLeftWidth = 0;
                    inner.style.overflow = Overflow.Visible;
                    inner.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                    var textEl = inner.Q<TextElement>();
                    if (textEl != null) { textEl.style.color = new Color(0.85f, 0.85f, 0.85f, 1f); }
                }
                // Defer so USS resolution doesn't overwrite it.
                _inputField.schedule.Execute(() => _inputField.textSelection.cursorColor = Color.white);
            });
            _inputField.RegisterCallback<KeyDownEvent>(OnKeyDown);
            // Prevent the field from losing focus while the input row is visible.
            _inputField.RegisterCallback<FocusOutEvent>(_ => {
                if (_inputRow.style.display == DisplayStyle.Flex) {
                    _inputField.schedule.Execute(() => _inputField.Focus());
                }
            });
            _inputRow.Add(_inputField);

            _chatContent.Add(_inputRow);
            _root.Add(_chatContent);

            _root.style.display = DisplayStyle.None;
            pageRoot.Add(_root);
        }

        internal bool IsInputRowVisible => _inputRow.style.display == DisplayStyle.Flex;

        internal void ClearLog() {
            _logScroll.Clear();
            _fadeTimer?.Pause();
            _fadeTimer = null;
            _root.style.display = DisplayStyle.None;
        }

        internal void AppendMessage(string sender, string text) {
            var label = new Label { text = $"[{sender}] {text}" };
            label.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            label.style.fontSize = 20;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingBottom = 3;
            label.pickingMode = PickingMode.Ignore;
            _logScroll.Add(label);
            _logScroll.schedule.Execute(() => _logScroll.scrollOffset = new Vector2(0, float.MaxValue)).StartingIn(50);

            if (_root.style.display == DisplayStyle.None) {
                _root.style.display = DisplayStyle.Flex;
            }
            RestartFadeTimer();
        }

        internal void ShowInputRow() {
            _fadeTimer?.Pause();
            _fadeTimer = null;
            _root.style.display = DisplayStyle.Flex;
            _root.pickingMode = PickingMode.Position; // intercept all clicks while typing
            _inputRow.style.display = DisplayStyle.Flex;
            _inputField.value = "";
            _inputField.textSelection.cursorColor = Color.white;
            _inputField.schedule.Execute(() => _inputField.Focus());
        }

        internal void HideInputRow() {
            _root.pickingMode = PickingMode.Ignore;
            _inputRow.style.display = DisplayStyle.None;
            _inputField.value = "";
            RestartFadeTimer();
        }

        internal void ForceHide() {
            _fadeTimer?.Pause();
            _fadeTimer = null;
            _root.pickingMode = PickingMode.Ignore;
            _inputRow.style.display = DisplayStyle.None;
            _inputField.value = "";
            _root.style.display = DisplayStyle.None;
        }

        private void RestartFadeTimer() {
            _fadeTimer?.Pause();
            _fadeTimer = _root.schedule.Execute(HideLog).StartingIn(8000);
        }

        private void HideLog() {
            _root.style.display = DisplayStyle.None;
        }

        private void OnKeyDown(KeyDownEvent evt) {
            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter) {
                evt.StopPropagation();
                OnSendRequested?.Invoke(_inputField.value);
            } else if (evt.keyCode == KeyCode.Escape) {
                // Stop UIElements propagation; actual close + pause-block handled via OnNavigateInput prefix.
                evt.StopPropagation();
            }
        }
    }
}
