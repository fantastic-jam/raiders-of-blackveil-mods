using RR.Input;
using RR.UI.Controls.Menu.Options;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace WildguardModFramework.PlayerManagement {
    /// <summary>Rebind row for the player list overlay key, shown in WMF → Players.</summary>
    internal static class PlayerListKeyBind {
        // The game's rebind UI needs a live InputAction; one is kept per open panel and
        // replaced on rebuild (sub-menus have no close hook).
        private static InputAction _inputAction;

        internal static void Build(VisualElement container, bool isInGameMenu) {
            _inputAction?.Disable();
            _inputAction?.Dispose();

            var keyName = WmfConfig.PlayerListKey.Value.ToString();
            _inputAction = new InputAction("WMF_PlayerListKey", InputActionType.Button);
            _inputAction.AddBinding("<Keyboard>/" + char.ToLower(keyName[0]) + keyName.Substring(1));
            _inputAction.Enable();

            var bindInfo = new InputBindInfo(
                _inputAction,
                "@" + WmfMod.t("players.overlay_key"),
                bindIndex: 0,
                RebindCategory.Menu,
                RebindOverlapGroup.Menu,
                RebindSaveMode.Static
            );

            var row = new OptionKeyBind();
            row.style.alignSelf = Align.Stretch;
            row.style.marginBottom = 16;
            row.Init(bindInfo, isGamepad: false);

            if (isInGameMenu) {
                row.Enabled = false;
            } else {
                row.OnRebindCompleted = _ => {
                    var segments = row.BindingPath.Split('/');
                    if (System.Enum.TryParse<Key>(segments[segments.Length - 1], ignoreCase: true, out var newKey)) {
                        WmfConfig.PlayerListKey.Value = newKey;
                    }
                };
            }

            container.Add(row);
        }
    }
}
