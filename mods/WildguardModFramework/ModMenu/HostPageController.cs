using System.Linq;
using System.Reflection;
using HarmonyLib;
using WildguardModFramework.Registry;
using RR.UI.Controls;
using RR.UI.Controls.Menu.JoinHost;
using RR.UI.Extensions;
using RR.UI.Pages;
using RR.UI.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Owns all UI state and behavior for the host start page stepper injection.
    /// Created on the first OnActivate; subsequent activations call Reset() on the existing instance.
    /// Instance fields hold VisualElement references that are valid for the lifetime of MenuStartHostPage.
    /// </summary>
    internal sealed class HostPageController {
        internal static HostPageController Current { get; private set; }

        // Reflection handles resolved once at class initialization.
        private static readonly FieldInfo CursorField = AccessTools.Field(typeof(MenuStartHostPage), "_cursor");
        private static readonly FieldInfo PrivacyField = AccessTools.Field(typeof(MenuStartHostPage), "_privacyStepper");
        private static readonly FieldInfo SessionInputField = AccessTools.Field(typeof(MenuStartHostPage), "_sessionNameTextInput");
        private static readonly FieldInfo JoinHostLabelField = AccessTools.Field(typeof(JoinHostControl), "_label");

        private static string[] StepperItems => new[] { WmfMod.t("stepper.yes"), WmfMod.t("stepper.no") };

        private readonly JoinHostStepper _allowModsStepper;
        private readonly JoinHostStepper _allowCheatsStepper;
        private readonly JoinHostStepper _gameModeStepper;
        private readonly Label _finalNameLabel;
        private readonly JoinHostTextInput _sessionInput;
        private VisualElement _tooltipPanel;
        private Label _tooltipLabel;

        internal bool AllowMods => _allowModsStepper == null || _allowModsStepper.Index == 0;
        internal bool AllowCheats => _allowCheatsStepper == null || _allowCheatsStepper.Index == 0;

        /// <summary>
        /// Called on every OnActivate. First call injects UI; subsequent calls reset stepper state.
        /// </summary>
        internal static void Activate(MenuStartHostPage page) {
            if (Current != null) {
                Current.Reset();
                return;
            }
            Current = new HostPageController(page);
        }

        private HostPageController(MenuStartHostPage page) {
            var enabledMods = ModScanner.Mods.Where(m => WmfConfig.IsEnabled(m.Guid)).ToList();
            var enabledCheats = ModScanner.Cheats.Where(m => WmfConfig.IsEnabled(m.Guid)).ToList();
            var gameModes = ModScanner.GameModes;

            bool hasMods = enabledMods.Count > 0;
            bool hasCheats = enabledCheats.Count > 0;
            bool hasGameModes = gameModes.Count > 0;

            if (!hasMods && !hasCheats && !hasGameModes) { return; }

            if (CursorField == null || PrivacyField == null || SessionInputField == null) {
                WmfMod.PublicLogger.LogWarning("WMF: host page fields not found — skipping UI injection.");
                return;
            }

            var cursor = CursorField.GetValue(page) as UICursorLinear<object>;
            var privacyStepper = PrivacyField.GetValue(page) as JoinHostStepper;
            _sessionInput = SessionInputField.GetValue(page) as JoinHostTextInput;

            if (cursor == null || privacyStepper == null) { return; }

            var container = privacyStepper.parent;
            if (container == null) { return; }
            int insertIndex = container.IndexOf(privacyStepper) + 1;

            BuildTooltip(page.RootElement);

            if (hasMods) {
                _allowModsStepper = CreateStepper(WmfMod.t("stepper.allow_mods"));
                RegisterTooltip(_allowModsStepper, string.Join("\n", enabledMods.Select(m => m.Name)));
                container.Insert(insertIndex++, _allowModsStepper);
                cursor.RegisterItem(_allowModsStepper);
                _allowModsStepper.OnValueChangedCallback = _ => UpdateFinalName();
            }

            if (hasCheats) {
                _allowCheatsStepper = CreateStepper(WmfMod.t("stepper.allow_cheats"));
                RegisterTooltip(_allowCheatsStepper, string.Join("\n", enabledCheats.Select(m => m.Name)));
                container.Insert(insertIndex++, _allowCheatsStepper);
                cursor.RegisterItem(_allowCheatsStepper);
                _allowCheatsStepper.OnValueChangedCallback = _ => UpdateFinalName();
            }

            if (hasGameModes) {
                var items = new string[gameModes.Count + 1];
                items[0] = WmfMod.t("gamemode.normal");
                for (int i = 0; i < gameModes.Count; i++) { items[i + 1] = gameModes[i].DisplayName; }

                _gameModeStepper = CreateStepper(WmfMod.t("stepper.game_mode"));
                _gameModeStepper.SetItems(items, GetCurrentGameModeIndex());
                container.Insert(insertIndex++, _gameModeStepper);
                cursor.RegisterItem(_gameModeStepper);
                _gameModeStepper.OnValueChangedCallback = idx => {
                    ModScanner.SelectedGameModeVariantId = idx == 0 ? null : gameModes[idx - 1].VariantId;
                    UpdateFinalName();
                };
            }

            _finalNameLabel = new Label();
            _finalNameLabel.style.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            _finalNameLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _finalNameLabel.style.paddingLeft = 8;
            _finalNameLabel.style.marginBottom = 4;

            if (_sessionInput != null) {
                var prevSessionCallback = _sessionInput.OnValueChangedCallback;
                _sessionInput.OnValueChangedCallback = inp => { prevSessionCallback?.Invoke(inp); UpdateFinalName(); };

                if (_sessionInput.parent == container) {
                    container.Insert(container.IndexOf(_sessionInput) + 1, _finalNameLabel);
                } else {
                    container.Insert(insertIndex, _finalNameLabel);
                }
            } else {
                container.Insert(insertIndex, _finalNameLabel);
            }

            UpdateFinalName();
        }

        private void Reset() {
            _allowModsStepper?.SetIndex(0);
            _allowCheatsStepper?.SetIndex(0);
            _gameModeStepper?.SetIndex(GetCurrentGameModeIndex());
            UpdateFinalName();
        }

        private void UpdateFinalName() {
            if (_finalNameLabel == null) { return; }

            bool cheatsActive = AllowCheats && ModScanner.Cheats.Any(m => WmfConfig.IsEnabled(m.Guid));
            bool modsActive = AllowMods && ModScanner.Mods.Any(m => WmfConfig.IsEnabled(m.Guid));

            var gameModeId = ModScanner.SelectedGameModeVariantId;
            var activeGameMode = gameModeId != null
                ? ModScanner.GameModes.FirstOrDefault(g => g.VariantId == gameModeId)
                : null;

            string suffix = activeGameMode != null ? " [" + activeGameMode.DisplayName + "]" : modsActive ? " (modded)" : "";
            if (cheatsActive) { suffix += " (cheats)"; }

            if (string.IsNullOrEmpty(suffix)) {
                _finalNameLabel.text = "";
                return;
            }

            string baseName = _sessionInput?.Value ?? "";
            int maxBase = SessionOrchestrator.SessionNameMaxLength - suffix.Length;
            if (maxBase < 0) { maxBase = 0; }
            if (baseName.Length > maxBase) { baseName = baseName.Substring(0, maxBase); }
            _finalNameLabel.text = string.IsNullOrEmpty(baseName) ? suffix.TrimStart() : baseName + suffix;
        }

        private static int GetCurrentGameModeIndex() {
            var id = ModScanner.SelectedGameModeVariantId;
            if (id == null) { return 0; }
            var idx = ModScanner.GameModes.FindIndex(g => g.VariantId == id);
            return idx < 0 ? 0 : idx + 1;
        }

        private void BuildTooltip(VisualElement pageRoot) {
            if (_tooltipPanel != null) { return; }

            _tooltipPanel = new VisualElement();
            _tooltipPanel.style.position = Position.Absolute;
            _tooltipPanel.style.backgroundColor = new Color(0.06f, 0.06f, 0.06f, 0.96f);
            _tooltipPanel.style.borderTopLeftRadius = 5;
            _tooltipPanel.style.borderTopRightRadius = 5;
            _tooltipPanel.style.borderBottomLeftRadius = 5;
            _tooltipPanel.style.borderBottomRightRadius = 5;
            var borderColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            _tooltipPanel.style.borderTopColor = borderColor;
            _tooltipPanel.style.borderBottomColor = borderColor;
            _tooltipPanel.style.borderLeftColor = borderColor;
            _tooltipPanel.style.borderRightColor = borderColor;
            _tooltipPanel.style.borderTopWidth = 1;
            _tooltipPanel.style.borderBottomWidth = 1;
            _tooltipPanel.style.borderLeftWidth = 1;
            _tooltipPanel.style.borderRightWidth = 1;
            _tooltipPanel.style.paddingTop = 12;
            _tooltipPanel.style.paddingBottom = 12;
            _tooltipPanel.style.paddingLeft = 16;
            _tooltipPanel.style.paddingRight = 16;
            _tooltipPanel.pickingMode = PickingMode.Ignore;
            _tooltipPanel.style.display = DisplayStyle.None;

            _tooltipLabel = new Label();
            _tooltipLabel.style.color = Color.white;
            _tooltipPanel.Add(_tooltipLabel);

            pageRoot.Add(_tooltipPanel);
        }

        private void RegisterTooltip(VisualElement target, string text) {
            target.RegisterCallback<MouseEnterEvent>(_ => ShowTooltip(target, text));
            target.RegisterCallback<MouseLeaveEvent>(_ => {
                if (_tooltipPanel != null) { _tooltipPanel.style.display = DisplayStyle.None; }
            });
        }

        private void ShowTooltip(VisualElement target, string text) {
            if (_tooltipPanel == null || _tooltipLabel == null) { return; }
            _tooltipLabel.text = text;

            var targetBound = target.worldBound;
            var parentBound = _tooltipPanel.parent.worldBound;
            _tooltipPanel.style.left = targetBound.x - parentBound.x;
            _tooltipPanel.style.top = StyleKeyword.Auto;
            _tooltipPanel.style.bottom = _tooltipPanel.parent.contentRect.height - (targetBound.y - parentBound.y) + 8f;
            _tooltipPanel.style.display = DisplayStyle.Flex;
        }

        private static JoinHostStepper CreateStepper(string labelText) {
            var stepper = new JoinHostStepper();
            stepper.SetItems(StepperItems, 0);
            if (JoinHostLabelField?.GetValue(stepper) is Label label) {
                label.text = labelText;
            }
            return stepper;
        }
    }
}
