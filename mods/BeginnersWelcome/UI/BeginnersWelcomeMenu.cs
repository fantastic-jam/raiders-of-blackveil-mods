using BepInEx.Configuration;
using RR.Input;
using RR.UI.Controls.Menu.Options;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BeginnersWelcome.UI {
    internal sealed class BeginnersWelcomeMenu {
        private readonly ConfigEntry<Key> _keyEntry;
        private readonly bool _isInGameMenu;
        private InputAction _inputAction;

        internal BeginnersWelcomeMenu(ConfigEntry<Key> keyEntry, bool isInGameMenu) {
            _keyEntry = keyEntry;
            _isInGameMenu = isInGameMenu;
        }

        internal void Build(VisualElement container) {
            var keyName = _keyEntry.Value.ToString();
            var path = "<Keyboard>/" + char.ToLower(keyName[0]) + keyName.Substring(1);

            _inputAction = new InputAction("BW_HCPanel", InputActionType.Button);
            _inputAction.AddBinding(path);
            _inputAction.Enable();

            var bindInfo = new InputBindInfo(
                _inputAction,
                "@Handicap Panel Toggle",
                bindIndex: 0,
                RebindCategory.Menu,
                RebindOverlapGroup.Menu,
                RebindSaveMode.Static
            );

            var row = new OptionKeyBind();
            row.style.alignSelf = Align.Stretch;
            row.Init(bindInfo, isGamepad: false);

            if (_isInGameMenu) {
                row.Enabled = false;
            } else {
                row.OnRebindCompleted = _ => {
                    var segments = row.BindingPath.Split('/');
                    var keyPart = segments[segments.Length - 1];
                    if (System.Enum.TryParse<Key>(keyPart, ignoreCase: true, out var newKey)) {
                        _keyEntry.Value = newKey;
                    }
                };
            }

            container.Add(row);
        }

        internal void Dispose() {
            _inputAction?.Disable();
            _inputAction?.Dispose();
            _inputAction = null;
        }
    }
}
