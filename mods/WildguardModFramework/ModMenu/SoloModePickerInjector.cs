using WildguardModFramework.Registry;
using RR.Input;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Injects a solo game-mode picker over the NewSinglePlayerGameButton and owns the overlay page.
    /// Called once from MenuStartPagePatch.OnInitPostfix when game modes are registered.
    /// Delegates to the original button handler so third-party wrappers (e.g. OfflineMode login) are preserved.
    /// </summary>
    internal static class SoloModePickerInjector {
        private static UIDynamicPageLayer<SoloStartPage> _soloPage;

        internal static void Inject(MenuStartPage instance) {
            if (ModScanner.GameModes.Count == 0) { return; }

            var soloBtn = instance.RootElement.Q<ButtonGeneric3>("NewSinglePlayerGameButton");
            if (soloBtn == null) {
                WmfMod.PublicLogger.LogWarning("WMF: NewSinglePlayerGameButton not found — solo game mode picker unavailable.");
                return;
            }

            var soloContainer = new VisualElement { name = "SoloStartContainer" };
            instance.RootElement.Add(soloContainer);

            // Capture before replacing — may include OfflineMode's EnsureLoggedIn wrapper.
            var originalOnClick = soloBtn.OnClick;

            _soloPage = new UIDynamicPageLayer<SoloStartPage>(soloContainer, instance.ParentPageLayer, 0f, 0f);
            _soloPage.Page.OnStartSolo = () => {
                _soloPage.Close(TransitionAnimation.None);
                // Delegate to original handler so login wrappers (e.g. OfflineMode) run first.
                originalOnClick?.Invoke(soloBtn);
            };
            _soloPage.Page.OnCloseRequest = () => _soloPage.Close(TransitionAnimation.None);

            soloBtn.OnClick = _ => _soloPage.Open(TransitionAnimation.None);
        }

        /// <summary>Returns false if the solo picker consumed the input (skip original handler).</summary>
        internal static bool HandleInput(InputPressEvent evt) {
            if (_soloPage is not { IsOpen: true }) { return false; }
            _soloPage.OnNavigateInput(evt);
            return true;
        }

    }
}
