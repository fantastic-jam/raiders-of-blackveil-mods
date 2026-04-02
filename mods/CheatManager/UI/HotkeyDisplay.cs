using RR.UI.Pages;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace CheatManager.UI {
    public static class HotkeyDisplay {
        private static readonly Dictionary<BaseHUDPage, HotkeyTooltip> _tooltips = new();
        private static BaseHUDPage _activePage;

        public static void OnPageInit(BaseHUDPage page) {
            var container = page.RootElement;
            if (container == null) {
                return;
            }


            var tooltip = new HotkeyTooltip(container);
            tooltip.AddEntry("+25% health", "H");
            tooltip.AddEntry("Heal all players", "U");
            tooltip.AddEntry("Kill all enemies", "L");
            tooltip.AddEntry("Trigger level exit", "Shift+L");
            tooltip.AddEntry("Restart same level", "Shift+K");
            tooltip.AddEntry("+200 Black Coins", "M");
            tooltip.AddEntry("+2500 Scrap", "Shift+M");
            tooltip.AddEntry("+50 XP all players", "N");
            tooltip.AddEntry("Set all-players-dead", "9");
            tooltip.AddEntry("Force vending machine", "Shift+B");
            tooltip.AddEntry("Damage player slot 0 / 1 / 2", "F4 / F11 / F12");
            _tooltips[page] = tooltip;
        }

        public static void OnPageUpdate(BaseHUDPage page) {
            _activePage = page;
            Update();
        }

        public static void Update() {
            if (_activePage == null || !_tooltips.TryGetValue(_activePage, out var tooltip)) {
                return;
            }


            if (Keyboard.current.altKey.wasPressedThisFrame) {
                tooltip.Show();
            }


            if (Keyboard.current.altKey.wasReleasedThisFrame) {
                tooltip.Hide();
            }

        }
    }
}
