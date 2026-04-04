using RR.Scripts.UI.Extensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace CheatManager.UI {
    public class HotkeyTooltip {
        private readonly VisualElement _panel;
        private readonly VisualElement _rowsHolder;

        public HotkeyTooltip(VisualElement container) {
            _panel = new VisualElement();
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
            _panel.VisibleDisplay(visible: false);

            _rowsHolder = new VisualElement();
            _rowsHolder.style.flexDirection = FlexDirection.Column;
            _panel.Add(_rowsHolder);

            container.Add(_panel);
        }

        public void AddEntry(string action, string keybind) {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 4;

            var keybindLabel = new Label(keybind);
            keybindLabel.style.color = Color.white;
            keybindLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            keybindLabel.style.minWidth = 100;

            var actionLabel = new Label(action);
            actionLabel.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            actionLabel.style.marginLeft = 16;

            row.Add(keybindLabel);
            row.Add(actionLabel);
            _rowsHolder.Add(row);
        }

        public void Show() => _panel.VisibleDisplay(visible: true);
        public void Hide() => _panel.VisibleDisplay(visible: false);
    }
}
