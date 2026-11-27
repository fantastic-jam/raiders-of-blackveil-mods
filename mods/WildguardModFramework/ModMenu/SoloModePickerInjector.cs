using System;
using WildguardModFramework.Registry;
using RR.Input;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Injects a solo game-mode picker over the NewSinglePlayerGameButton and owns the overlay.
    /// Called from MenuStartPagePatch.CtorPostfix when game modes are registered.
    /// Delegates to the original button handler so third-party wrappers (e.g. OfflineMode login) are preserved.
    /// </summary>
    internal static class SoloModePickerInjector {
        private static SoloModePicker _picker;

        internal static void Inject(MenuStartPage instance) {
            _picker = null;
            if (ModScanner.GameModes.Count == 0) { return; }

            var soloBtn = instance.RootElement.Q<ButtonMenuStart>("NewSinglePlayerGameButton");
            if (soloBtn == null) {
                WmfMod.PublicLogger.LogWarning("WMF: NewSinglePlayerGameButton (ButtonMenuStart) not found — solo game mode picker unavailable.");
                return;
            }

            // Capture before replacing — may include OfflineMode's EnsureLoggedIn wrapper.
            var originalOnClick = soloBtn.OnClick;

            var picker = new SoloModePicker(instance);
            // Delegate to original handler so login wrappers (e.g. OfflineMode) run first.
            picker.OnStartSolo = () => originalOnClick?.Invoke(soloBtn);
            _picker = picker;

            soloBtn.OnClick = (Action<ButtonMenuStart>)(_ => picker.Open());
        }

        /// <summary>Returns false if the solo picker consumed the input (skip original handler).</summary>
        internal static bool HandleInput(InputPressEvent evt) {
            if (_picker is not { IsOpen: true }) { return false; }
            _picker.OnNavigateInput(evt);
            return true;
        }
    }
}
