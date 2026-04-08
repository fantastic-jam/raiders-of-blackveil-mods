using WildguardModFramework.Registry;
using RR;
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
    /// Also replicates MenuStartPage.StartClick(SinglePlayer) for bypassed single-player launches.
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

            _soloPage = new UIDynamicPageLayer<SoloStartPage>(soloContainer, instance.ParentPageLayer, 0f, 0f);
            _soloPage.Page.OnStartSolo = () => {
                _soloPage.Close(TransitionAnimation.None);
                StartSoloGame();
            };
            _soloPage.Page.OnCloseRequest = () => _soloPage.Close(TransitionAnimation.None);

            soloBtn.OnClick = _ => {
                if (ModScanner.GameModes.Count == 0) {
                    StartSoloGame();
                } else {
                    _soloPage.Open(TransitionAnimation.None);
                }
            };
        }

        /// <summary>Returns false if the solo picker consumed the input (skip original handler).</summary>
        internal static bool HandleInput(InputPressEvent evt) {
            if (_soloPage is not { IsOpen: true }) { return false; }
            _soloPage.OnNavigateInput(evt);
            return true;
        }

        /// <summary>
        /// Replicates MenuStartPage.StartClick(PlaySessionMode.SinglePlayer).
        /// Note: AudioManager.PlayUISound is omitted — it requires FMODUnity which is not referenced.
        /// </summary>
        private static void StartSoloGame() {
            AudioManager.Instance?.StopMainMenuAudio();
            UIManager.Instance?.CloseHUD();
            UIManager.Instance?.ChangePage("LoadingGamePage", TransitionAnimation.Fade, false, () => {
                BackendManager.Instance?.BeginPlaySession("", "", BackendManager.PlaySessionMode.SinglePlayer, false);
            });
        }
    }
}
