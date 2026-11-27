using System;
using RR.Input;
using RR.UI.Components;
using RR.UI.Controls.Menu.JoinHost;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine;
using UnityEngine.UIElements;
using WildguardModFramework.Registry;
using WildguardModFramework.Ui;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Solo game-mode picker shown over the main menu before starting a solo run.
    ///
    /// IL2CPP: this used to be a PageBase subclass hosted in a UIDynamicPageLayer. The game creates
    /// pages natively via Activator.CreateInstance(type, PageArgs), and an injected managed class only
    /// exposes a parameterless constructor to IL2CPP — so it is now a plain managed overlay (same
    /// approach as ModsMenuOverlay) hosting the game's native JoinHostStepper.
    /// </summary>
    internal sealed class SoloModePicker {
        private readonly VisualElement _root;
        private readonly JoinHostStepper _gameModeStepper;

        internal Action OnStartSolo { get; set; }
        internal bool IsOpen { get; private set; }

        internal SoloModePicker(MenuStartPage page) {
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.top = _root.style.right = _root.style.bottom = _root.style.left = 0;
            _root.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.92f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.display = DisplayStyle.None;

            var panel = new VisualElement();
            panel.style.width = 560;
            panel.style.paddingTop = panel.style.paddingBottom = 24;
            panel.style.paddingLeft = panel.style.paddingRight = 32;
            panel.style.backgroundColor = new Color(0.09f, 0.09f, 0.09f, 1f);

            var title = new Label { text = WmfMod.t("solo.title") };
            title.style.color = Color.white;
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            panel.Add(title);

            _gameModeStepper = new JoinHostStepper { ParentPage = page.Cast<IAllowClick>() };
            _gameModeStepper._label?.SetLiteral("", customTransform: (Func<string, string>)(_ => WmfMod.t("stepper.game_mode")));
            panel.Add(_gameModeStepper);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 20;
            buttons.Add(MakeButton(WmfMod.t("solo.back"), Close));
            buttons.Add(MakeButton(WmfMod.t("solo.start"), ConfirmAndStart));
            panel.Add(buttons);

            _root.Add(panel);
            page.RootElement.Add(_root);
        }

        internal void Open() {
            RefreshStepper();
            _gameModeStepper.Hovered = true;
            _root.style.display = DisplayStyle.Flex;
            IsOpen = true;
        }

        internal void Close() {
            _root.style.display = DisplayStyle.None;
            IsOpen = false;
        }

        internal void OnNavigateInput(InputPressEvent evt) {
            if (!evt.IsPressed) { return; }
            switch (evt.Type) {
                case PageNavType.NavigateLeft:
                    _gameModeStepper.NavigateLeft();
                    break;
                case PageNavType.NavigateRight:
                case PageNavType.SpecialX:
                    _gameModeStepper.NavigateRight();
                    break;
                case PageNavType.Submit:
                    ConfirmAndStart();
                    break;
                case PageNavType.Cancel:
                    Close();
                    break;
            }
        }

        private void ConfirmAndStart() {
            var idx = _gameModeStepper.Index;
            var gameModes = ModScanner.GameModes;
            ModScanner.SelectedGameModeVariantId = idx == 0 ? null : gameModes[idx - 1].VariantId;

            // Activate the chosen game mode now — all modes are disabled while in the main menu.
            // BeginPlaySessionPrefix only manages game modes for host sessions.
            foreach (var gm in gameModes) { gm.Disable(); }
            if (idx > 0) { gameModes[idx - 1].Enable(); }
            WmfConfig.ActiveGameModeId = ModScanner.SelectedGameModeVariantId ?? "";

            Close();
            OnStartSolo?.Invoke();
        }

        private void RefreshStepper() {
            var gameModes = ModScanner.GameModes;
            var items = new string[gameModes.Count + 1];
            items[0] = WmfMod.t("gamemode.normal");
            for (int i = 0; i < gameModes.Count; i++) { items[i + 1] = gameModes[i].DisplayName; }

            var currentId = ModScanner.SelectedGameModeVariantId;
            var currentIdx = 0;
            if (currentId != null) {
                var found = gameModes.FindIndex(g => g.VariantId == currentId);
                if (found >= 0) { currentIdx = found + 1; }
            }
            _gameModeStepper.SetItems(items, currentIdx);
            _gameModeStepper._label?.Refresh();
        }

        private static Label MakeButton(string text, Action onClick) {
            var btn = new Label { text = text };
            btn.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.style.fontSize = 15;
            btn.style.marginLeft = 16;
            btn.style.paddingTop = btn.style.paddingBottom = 6;
            btn.style.paddingLeft = btn.style.paddingRight = 14;
            btn.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            btn.On<ClickEvent>(_ => onClick());
            btn.On<MouseEnterEvent>(_ => btn.style.color = Color.white);
            btn.On<MouseLeaveEvent>(_ => btn.style.color = new Color(0.85f, 0.85f, 0.85f, 1f));
            return btn;
        }
    }
}
