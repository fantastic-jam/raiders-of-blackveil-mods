using BepInEx.Configuration;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu.Options;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace BeginnersWelcome.UI {
    internal sealed class BeginnersWelcomeMenu {
        private readonly ConfigEntry<Key> _keyEntry;
        private readonly bool _isInGameMenu;
        private LocLabel _btnLabel;
        private InputActionRebindingExtensions.RebindingOperation _rebindOp;
        private InputAction _tempAction;

        internal BeginnersWelcomeMenu(ConfigEntry<Key> keyEntry, bool isInGameMenu) {
            _keyEntry = keyEntry;
            _isInGameMenu = isInGameMenu;
        }

        internal void Build(VisualElement container) {
            var row = new OptionButton();
            var lbl = row.Q<LocLabel>("Label");
            if (lbl != null) { lbl.CustomTransform = _ => "Handicap Panel Toggle"; lbl.Refresh(); }

            _btnLabel = row.Q<LocLabel>("Button");
            RefreshButtonLabel();

            if (!_isInGameMenu) {
                row.OnClickedEvent = StartRebind;
            } else {
                row.SetEnabled(false);
            }

            container.Add(row);
        }

        private void StartRebind() {
            if (_rebindOp != null) { return; }

            SetButtonText("...");

            if (InputManager.Instance != null) {
                InputManager.Instance.UIActionMap.Enabled = false;
            }

            _tempAction = new InputAction("BW_Rebind", InputActionType.Button);
            _tempAction.AddBinding("<Keyboard>/f3"); // placeholder, will be overridden
            // Keep the action disabled — PerformInteractiveRebinding works on disabled actions

            _rebindOp = _tempAction
                .PerformInteractiveRebinding(0)
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Keyboard>/enter")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => FinishRebind(op, complete: true))
                .OnCancel(op => FinishRebind(op, complete: false))
                .Start();
        }

        private void FinishRebind(InputActionRebindingExtensions.RebindingOperation op, bool complete) {
            if (complete && op.selectedControl is KeyControl kc) {
                _keyEntry.Value = kc.keyCode;
            }

            op.Dispose();
            _rebindOp = null;
            _tempAction?.Disable();
            _tempAction?.Dispose();
            _tempAction = null;

            if (InputManager.Instance != null) {
                InputManager.Instance.UIActionMap.Enabled = true;
            }

            RefreshButtonLabel();
        }

        internal void Dispose() {
            if (_rebindOp != null) {
                _rebindOp.Cancel();
                // FinishRebind will be called via OnCancel
            }
        }

        private void RefreshButtonLabel() => SetButtonText(_keyEntry.Value.ToString());

        private void SetButtonText(string text) {
            if (_btnLabel == null) { return; }
            _btnLabel.CustomTransform = _ => text;
            _btnLabel.Refresh();
        }
    }
}
