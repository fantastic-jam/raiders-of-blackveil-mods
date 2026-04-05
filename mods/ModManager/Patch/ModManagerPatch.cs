using System.Linq;
using HarmonyLib;
using RR;
using RR.UI.Controls;
using RR.UI.Controls.Menu.JoinHost;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModManager.Patch {
    internal static class ModManagerPatch {
        // Both " (cheats)" and " (modded)" are 9 chars.
        private const int SuffixLength = 9;

        private static readonly string[] StepperItems = { "Yes", "No" };

        // Stepper references set on first OnActivate, consumed in BeginPlaySession.
        // Index 0 = "Yes" (allow), Index 1 = "No" (disable).
        private static JoinHostStepper _allowModsStepper;
        private static JoinHostStepper _allowCheatsStepper;

        private static VisualElement _tooltipPanel;
        private static Label _tooltipLabel;

        // Final-name preview label — shows "{sessionName} (cheats)" etc.
        private static Label _finalNameLabel;

        // Kept so the update delegate can read the current input value.
        private static JoinHostTextInput _sessionInput;

        internal static void Apply(Harmony harmony) {
            var onActivate = AccessTools.Method(typeof(MenuStartHostPage), "OnActivate");
            if (onActivate == null) {
                ModManagerMod.PublicLogger.LogWarning("ModManager: MenuStartHostPage.OnActivate not found — host page UI patch inactive.");
            } else {
                harmony.Patch(onActivate, postfix: new HarmonyMethod(typeof(ModManagerPatch), nameof(OnActivatePostfix)));
            }

            var beginSession = AccessTools.Method(typeof(BackendManager), nameof(BackendManager.BeginPlaySession));
            if (beginSession == null) {
                ModManagerMod.PublicLogger.LogWarning("ModManager: BackendManager.BeginPlaySession not found — session tag and disable patch inactive.");
            } else {
                harmony.Patch(beginSession, prefix: new HarmonyMethod(typeof(ModManagerPatch), nameof(BeginPlaySessionPrefix)));
            }
        }

        // First open: scans, injects steppers + final-name hint.
        // Subsequent opens: rescans, resets stepper indices, refreshes hint.
        private static void OnActivatePostfix(MenuStartHostPage __instance) {
            ModManagerRegistrants.Scan();

            if (_allowModsStepper != null || _allowCheatsStepper != null) {
                _allowModsStepper?.SetIndex(0);
                _allowCheatsStepper?.SetIndex(0);
                UpdateFinalName();
                return;
            }

            bool hasMods = ModManagerRegistrants.Mods.Count > 0;
            bool hasCheats = ModManagerRegistrants.Cheats.Count > 0;
            if (!hasMods && !hasCheats) { return; }

            var cursorField = AccessTools.Field(typeof(MenuStartHostPage), "_cursor");
            var privacyField = AccessTools.Field(typeof(MenuStartHostPage), "_privacyStepper");
            var sessionInputField = AccessTools.Field(typeof(MenuStartHostPage), "_sessionNameTextInput");

            if (cursorField == null || privacyField == null || sessionInputField == null) {
                ModManagerMod.PublicLogger.LogWarning("ModManager: host page fields not found — skipping UI injection.");
                return;
            }

            var cursor = cursorField.GetValue(__instance) as UICursorLinear<object>;
            var privacyStepper = privacyField.GetValue(__instance) as JoinHostStepper;
            _sessionInput = sessionInputField.GetValue(__instance) as JoinHostTextInput;

            if (cursor == null || privacyStepper == null) { return; }

            var container = privacyStepper.parent;
            if (container == null) { return; }
            int insertIndex = container.IndexOf(privacyStepper) + 1;

            // Build tooltip from TutorialInputPrompt template, anchored to the page root
            // so worldBound coordinates map correctly onto the absolute-positioned overlay.
            BuildTooltip(__instance.RootElement);

            if (hasMods) {
                var names = ModManagerRegistrants.Mods.Select(m => m.Name).ToList();
                _allowModsStepper = CreateStepper("ALLOW MODS");
                RegisterTooltip(_allowModsStepper, string.Join("\n", names));
                container.Insert(insertIndex++, _allowModsStepper);
                cursor.RegisterItem(_allowModsStepper);
                _allowModsStepper.OnValueChangedCallback = _ => UpdateFinalName();
            }

            if (hasCheats) {
                var names = ModManagerRegistrants.Cheats.Select(m => m.Name).ToList();
                _allowCheatsStepper = CreateStepper("ALLOW CHEATS");
                RegisterTooltip(_allowCheatsStepper, string.Join("\n", names));
                container.Insert(insertIndex++, _allowCheatsStepper);
                cursor.RegisterItem(_allowCheatsStepper);
                _allowCheatsStepper.OnValueChangedCallback = _ => UpdateFinalName();
            }

            // Final-name preview: "{session name} (cheats)" shown below the session name input.
            _finalNameLabel = new Label();
            _finalNameLabel.style.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            _finalNameLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            _finalNameLabel.style.paddingLeft = 8;
            _finalNameLabel.style.marginBottom = 4;

            if (_sessionInput != null) {
                _sessionInput.MaxLength -= SuffixLength;

                // Hook session name changes to keep the preview current.
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

        private static void UpdateFinalName() {
            if (_finalNameLabel == null) { return; }

            bool cheatsActive = (_allowCheatsStepper == null || _allowCheatsStepper.Index == 0)
                                && ModManagerRegistrants.Cheats.Count > 0;
            bool modsActive = (_allowModsStepper == null || _allowModsStepper.Index == 0)
                              && ModManagerRegistrants.Mods.Count > 0;

            string suffix = cheatsActive ? " (cheats)" : modsActive ? " (modded)" : "";
            if (string.IsNullOrEmpty(suffix)) {
                _finalNameLabel.text = "";
                return;
            }

            string baseName = _sessionInput?.Value ?? "";
            _finalNameLabel.text = string.IsNullOrEmpty(baseName) ? suffix.TrimStart() : baseName + suffix;
        }

        // Disables unchecked mod types and appends the appropriate session name suffix.
        // Runs only after form validation passes, so Disable() is never called on a cancelled host.
        private static void BeginPlaySessionPrefix(ref string sessionTag) {
            bool allowMods = _allowModsStepper == null || _allowModsStepper.Index == 0;
            bool allowCheats = _allowCheatsStepper == null || _allowCheatsStepper.Index == 0;

            ModManagerMod.PublicLogger.LogInfo(
                $"ModManager: BeginPlaySession — allowMods={allowMods} (stepper={((_allowModsStepper == null) ? "null" : _allowModsStepper.Index.ToString())}), " +
                $"allowCheats={allowCheats} (stepper={((_allowCheatsStepper == null) ? "null" : _allowCheatsStepper.Index.ToString())})"
            );

            foreach (var mod in ModManagerRegistrants.Cheats) {
                if (allowCheats) {
                    ModManagerMod.PublicLogger.LogInfo($"ModManager: enabling cheat — {mod.Name}");
                    mod.Enable();
                } else {
                    ModManagerMod.PublicLogger.LogInfo($"ModManager: disabling cheat — {mod.Name}");
                    mod.Disable();
                }
            }
            foreach (var mod in ModManagerRegistrants.Mods) {
                if (allowMods) {
                    ModManagerMod.PublicLogger.LogInfo($"ModManager: enabling mod — {mod.Name}");
                    mod.Enable();
                } else {
                    ModManagerMod.PublicLogger.LogInfo($"ModManager: disabling mod — {mod.Name}");
                    mod.Disable();
                }
            }

            bool activeCheats = allowCheats && ModManagerRegistrants.Cheats.Count > 0;
            bool activeMods = allowMods && ModManagerRegistrants.Mods.Count > 0;

            if (activeCheats) {
                sessionTag += " (cheats)";
            } else if (activeMods) {
                sessionTag += " (modded)";
            }
        }

        private static void BuildTooltip(VisualElement pageRoot) {
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

        private static void RegisterTooltip(VisualElement target, string text) {
            target.RegisterCallback<MouseEnterEvent>(_ => ShowTooltip(target, text));
            target.RegisterCallback<MouseLeaveEvent>(_ => {
                if (_tooltipPanel != null) { _tooltipPanel.style.display = DisplayStyle.None; }
            });
        }

        private static void ShowTooltip(VisualElement target, string text) {
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

            var labelField = AccessTools.Field(typeof(JoinHostControl), "_label");
            if (labelField?.GetValue(stepper) is Label label) {
                label.text = labelText;
            }

            return stepper;
        }
    }
}
