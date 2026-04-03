using RR;
using RR.Scripts.UI.Extensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace BeginnersWelcome.UI {
    public class HandicapPanel {
        private readonly VisualElement _panel;
        private readonly VisualElement _rows;
        private readonly bool _isLobby;
        private bool _visible;

        public HandicapPanel(VisualElement container, bool isLobby = false) {
            _isLobby = isLobby;

            _panel = new VisualElement();
            _panel.pickingMode = PickingMode.Position;
            _panel.style.position = Position.Absolute;
            _panel.style.top = 12;
            _panel.style.left = new StyleLength(new Length(50f, LengthUnit.Percent));
            _panel.style.translate = new StyleTranslate(new Translate(new Length(-50f, LengthUnit.Percent), new Length(0f)));
            _panel.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.96f);
            _panel.style.borderTopLeftRadius = 5;
            _panel.style.borderTopRightRadius = 5;
            _panel.style.borderBottomLeftRadius = 5;
            _panel.style.borderBottomRightRadius = 5;
            _panel.style.borderTopWidth = 1;
            _panel.style.borderBottomWidth = 1;
            _panel.style.borderLeftWidth = 1;
            _panel.style.borderRightWidth = 1;
            var borderColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            _panel.style.borderTopColor = borderColor;
            _panel.style.borderBottomColor = borderColor;
            _panel.style.borderLeftColor = borderColor;
            _panel.style.borderRightColor = borderColor;
            _panel.style.paddingTop = 12;
            _panel.style.paddingBottom = 12;
            _panel.style.paddingLeft = 16;
            _panel.style.paddingRight = 16;
            _panel.style.width = isLobby ? 380 : 260;
            _panel.VisibleDisplay(visible: false);

            var title = new Label("Handicap  [F3]");
            title.style.color = Color.white;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 8;
            _panel.Add(title);

            _rows = new VisualElement();
            _rows.pickingMode = PickingMode.Position;
            _rows.style.flexDirection = FlexDirection.Column;
            _panel.Add(_rows);

            container.Add(_panel);
        }

        public void Toggle() {
            if (NetworkManager.Instance?.NetworkRunner?.IsSinglePlayer ?? false)
                return;
            _visible = !_visible;
            if (_visible)
                Refresh();
            _panel.VisibleDisplay(visible: _visible);
        }

        public void Hide() {
            _visible = false;
            _panel.VisibleDisplay(visible: false);
        }

        private static bool IsSessionHost() {
            var runner = NetworkManager.Instance?.NetworkRunner;
            if (runner == null) return true;
            return runner.IsSharedModeMasterClient || runner.IsServer || runner.IsSinglePlayer;
        }

        private void Refresh() {
            _rows.Clear();

            var players = PlayerManager.Instance?.GetPlayers();
            if (players == null || players.Count == 0) {
                var empty = new Label("No players connected.");
                empty.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
                _rows.Add(empty);
                return;
            }

            bool readOnly = !_isLobby || !IsSessionHost();

            foreach (var player in players) {
                int slot = player.SlotIndex;
                _rows.Add(readOnly ? BuildReadOnlyRow(player.UserName, slot) : BuildSliderRow(player.UserName, slot));
            }
        }

        private VisualElement BuildSliderRow(string userName, int slot) {
            var row = new VisualElement();
            row.pickingMode = PickingMode.Position;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var nameLabel = new Label(userName);
            nameLabel.style.color = Color.white;
            nameLabel.style.width = 140;
            nameLabel.style.flexShrink = 0;

            var slider = new SliderInt(-10, 10);
            slider.pickingMode = PickingMode.Position;
            slider.style.width = 180;
            slider.style.flexShrink = 0;
            slider.value = HandicapState.Values.TryGetValue(slot, out var stored) ? stored : 0;
            slider.RegisterValueChangedCallback(evt => HandicapState.Values[slot] = evt.newValue);

            var valueLabel = new Label(slider.value.ToString("+0;-0;0"));
            valueLabel.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            valueLabel.style.width = 28;
            valueLabel.style.flexShrink = 0;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            slider.RegisterValueChangedCallback(evt => valueLabel.text = evt.newValue.ToString("+0;-0;0"));

            row.Add(nameLabel);
            row.Add(slider);
            row.Add(valueLabel);
            return row;
        }

        private VisualElement BuildReadOnlyRow(string userName, int slot) {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var nameLabel = new Label(userName);
            nameLabel.style.color = Color.white;
            nameLabel.style.width = 180;
            nameLabel.style.flexShrink = 0;

            int handicap = HandicapState.Values.TryGetValue(slot, out var v) ? v : 0;
            var valueLabel = new Label(handicap.ToString("+0;-0;0"));
            valueLabel.style.color = handicap > 0
                ? new Color(0.4f, 0.9f, 0.4f, 1f)
                : handicap < 0
                    ? new Color(0.9f, 0.4f, 0.4f, 1f)
                    : new Color(0.6f, 0.6f, 0.6f, 1f);
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            valueLabel.style.flexGrow = 1;

            row.Add(nameLabel);
            row.Add(valueLabel);
            return row;
        }
    }
}
