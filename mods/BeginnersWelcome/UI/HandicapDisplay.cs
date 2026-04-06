using RR.UI.Pages;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace BeginnersWelcome.UI {
    public static class HandicapDisplay {
        private static readonly Dictionary<BaseHUDPage, HandicapPanel> _panels = new();
        private static BaseHUDPage _activePage;

        public static void OnPageInit(BaseHUDPage page) {
            var container = page.RootElement;
            if (container == null) {
                return;
            }


            if (page is LobbyHUDPage) {
                _panels[page] = new HandicapPanel(container, isLobby: true);
            } else if (page is GameHUDPage) {
                _panels[page] = new HandicapPanel(container, isLobby: false);
            }
        }

        public static void OnPageUpdate(BaseHUDPage page) {
            _activePage = page;
            Update();
        }

        public static void OnPageDeactivate(BaseHUDPage page) {
            if (_panels.TryGetValue(page, out var panel)) {
                panel.Hide();
            }

            if (_activePage == page) {
                _activePage = null;
            }

        }

        public static void Update() {
            if (_activePage == null || !_panels.TryGetValue(_activePage, out var panel)) {
                return;
            }


            if (Keyboard.current[BeginnersWelcomeMod.PanelToggleKey.Value].wasPressedThisFrame) {
                panel.Toggle();
            }

        }
    }
}
