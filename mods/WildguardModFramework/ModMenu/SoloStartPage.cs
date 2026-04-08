using HarmonyLib;
using WildguardModFramework.Registry;
using RR.Input;
using RR.UI.Components;
using RR.UI.Controls;
using RR.UI.Controls.Keys;
using RR.UI.Controls.Menu;
using RR.UI.Controls.Menu.JoinHost;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// A solo-mode game launcher that reuses the MenuStartHostPage UXML layout.
    /// Clears the host-specific form controls and injects only the game mode stepper.
    /// </summary>
    internal sealed class SoloStartPage : PageBase {
        // Load the exact same UXML as MenuStartHostPage — same visual chrome, different content.
        public override string Name => "MenuStartHostPage";

        public System.Action OnStartSolo { get; set; }
        public System.Action OnCloseRequest { get; set; }

        private readonly UICursorLinear<object> _cursor = new(CursorInputType.ArrowsAndStickOnly);
        private JoinHostStepper _gameModeStepper;
        private JoinHostKeysFooter _keysFooter;

        protected override void OnInit() {
            _cursor.SelectFirstOnFirstInput = true;
            _cursor.DisableSubmit = true; // Submit is handled at the page level, not by the cursor

            // ── Title ─────────────────────────────────────────────────────────
            var title = RootElement.Q<LocLabel>("Title");
            if (title != null) {
                title.CustomTransform = _ => "SOLO GAME";
                title.Refresh();
            }

            // ── Clear form controls and inject game mode stepper ──────────────
            var controls = RootElement.Q("Controls");
            if (controls != null) {
                controls.Clear();
                _gameModeStepper = CreateGameModeStepper();
                controls.Add(_gameModeStepper);
                _cursor.RegisterItem(_gameModeStepper);
            } else {
                WmfMod.PublicLogger.LogWarning("WMF: SoloStartPage — #Controls not found; game mode stepper skipped.");
            }

            // ── Hide the "strongest PC" hint in #Footer ───────────────────────
            var footer = RootElement.Q("Footer");
            if (footer != null) {
                foreach (var el in footer.Children()) {
                    if (el is LocLabel) { el.style.display = DisplayStyle.None; }
                }
            }

            // ── Wire up Start button ──────────────────────────────────────────
            var startBtn = RootElement.Q<ButtonPopup>("StartButton");
            if (startBtn != null) {
                startBtn.OnClicked = _ => ConfirmAndStart();
            }

            // ── Wire up Keys footer (handles ESC / Cancel) ────────────────────
            _keysFooter = RootElement.Q<JoinHostKeysFooter>("KeysFooter");
            if (_keysFooter != null) {
                _keysFooter.OnCloseClicked = () => {
                    if (ParentPageLayer.IsOpenAndReady) {
                        OnCloseRequest?.Invoke();
                    }
                };
            }
        }

        public override void OnActivate() {
            RefreshStepper();
            _cursor.ResetSelection(InputManager.Instance.InputDeviceType.IsKeyboard());
        }

        public override void OnNavigateInput(InputPressEvent evt) {
            _cursor.OnNavigateInput(evt);
            _keysFooter?.OnNavigateInput(evt); // handles PageNavType.Cancel → OnCloseClicked

            if (!evt.IsPressed) { return; }
            switch (evt.Type) {
                case PageNavType.SpecialX:
                    _cursor.SelectedItem?.Element?.Submit();
                    break;
                case PageNavType.Submit:
                    ConfirmAndStart();
                    break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ConfirmAndStart() {
            if (_gameModeStepper != null) {
                var idx = _gameModeStepper.Index;
                var gameModes = ModScanner.GameModes;
                ModScanner.SelectedGameModeVariantId = idx == 0 ? null : gameModes[idx - 1].VariantId;

                // Activate the chosen game mode now — all modes are disabled while in the main menu.
                // BeginPlaySessionPrefix only manages game modes for host sessions.
                foreach (var gm in gameModes) { gm.Disable(); }
                if (idx > 0) { gameModes[idx - 1].Enable(); }
                WmfConfig.ActiveGameModeId = ModScanner.SelectedGameModeVariantId ?? "";
            }
            OnStartSolo?.Invoke();
        }

        private void RefreshStepper() {
            if (_gameModeStepper == null) { return; }
            var gameModes = ModScanner.GameModes;
            var items = new string[gameModes.Count + 1];
            items[0] = "Normal";
            for (int i = 0; i < gameModes.Count; i++) { items[i + 1] = gameModes[i].DisplayName; }

            var currentId = ModScanner.SelectedGameModeVariantId;
            var currentIdx = 0;
            if (currentId != null) {
                var found = gameModes.FindIndex(g => g.VariantId == currentId);
                if (found >= 0) { currentIdx = found + 1; }
            }
            _gameModeStepper.SetItems(items, currentIdx);
        }

        private static JoinHostStepper CreateGameModeStepper() {
            var stepper = new JoinHostStepper();
            var labelField = AccessTools.Field(typeof(JoinHostControl), "_label");
            if (labelField?.GetValue(stepper) is LocLabel lbl) {
                lbl.CustomTransform = _ => "GAME MODE";
                lbl.Refresh();
            }
            return stepper;
        }
    }
}
