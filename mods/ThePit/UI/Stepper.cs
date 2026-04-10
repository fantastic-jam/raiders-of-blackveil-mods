using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThePit.UI {
    // A labeled stepper row: [LABEL]  [<]  [value]  [>]
    // Wraps around at the ends. Fully self-contained; read Index or Value<T> after the user interacts.
    internal sealed class Stepper<T> {
        private readonly (string Label, T Value)[] _options;
        private readonly Label _valueLabel;
        private int _index;

        internal int Index => _index;
        internal T Value => _options[_index].Value;
        internal VisualElement Root { get; }

        internal Stepper(string rowLabel, (string Label, T Value)[] options, int initialIndex) {
            _options = options;
            _index = initialIndex;

            var row = new VisualElement();
            row.pickingMode = PickingMode.Position;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 14;

            var label = new Label { text = rowLabel };
            label.pickingMode = PickingMode.Ignore;
            label.style.width = 120;
            label.style.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            label.style.fontSize = 12;
            label.style.letterSpacing = 1f;

            var leftBtn = MakeStepButton("<", () => Step(-1));
            _valueLabel = new Label { text = _options[_index].Label };
            _valueLabel.pickingMode = PickingMode.Ignore;
            _valueLabel.style.width = 90;
            _valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _valueLabel.style.color = Color.white;
            _valueLabel.style.fontSize = 13;
            var rightBtn = MakeStepButton(">", () => Step(1));

            row.Add(label);
            row.Add(leftBtn);
            row.Add(_valueLabel);
            row.Add(rightBtn);
            Root = row;
        }

        internal void SetIndex(int idx) {
            _index = (idx + _options.Length) % _options.Length;
            _valueLabel.text = _options[_index].Label;
        }

        private void Step(int delta) {
            _index = (_index + delta + _options.Length) % _options.Length;
            _valueLabel.text = _options[_index].Label;
        }

        private static Button MakeStepButton(string text, Action clicked) {
            var btn = new Button(clicked) { text = text };
            btn.pickingMode = PickingMode.Position;
            btn.style.width = 26;
            btn.style.height = 26;
            btn.style.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.style.fontSize = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            btn.style.borderTopColor = btn.style.borderRightColor =
                btn.style.borderBottomColor = btn.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            btn.style.borderTopWidth = btn.style.borderRightWidth =
                btn.style.borderBottomWidth = btn.style.borderLeftWidth = 1;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 3;
            btn.style.marginLeft = btn.style.marginRight = 4;
            return btn;
        }
    }
}
