using HarmonyLib;
using System;
using System.Reflection;

namespace DisableSkillsBar.Patch {
    public static class DisableSkillsBarPatch {
        private static bool _loggedLegacyInputUnavailable;
        private static bool _inputSystemInitAttempted;
        private static bool _inputSystemAvailable;
        private static PropertyInfo _keyboardCurrentProp;
        private static PropertyInfo _leftAltKeyProp;
        private static PropertyInfo _rightAltKeyProp;
        private static PropertyInfo _keyIsPressedProp;

        public static void Apply(Harmony harmony) {
            var abilityBarType = AccessTools.TypeByName("RR.UI.Controls.HUD.AbilityBar");
            if (abilityBarType == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning("Could not find RR.UI.Controls.HUD.AbilityBar in current game build. Patch is inactive.");
                return;
            }

            var checkInputMethod = AccessTools.Method(abilityBarType, "CheckInput");
            if (checkInputMethod == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no CheckInput method to patch.");
                return;
            }
            harmony.Patch(checkInputMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(DisableSkillsBarPatch), nameof(CheckInputPrefix))));

            // SetUpgradeIndex(int, bool mouseHover) sets UpgradeMode=true directly on hover,
            // causing a flash + SFX before CheckInput can reset it. Block it unless Alt is held.
            var setUpgradeIndexMethod = AccessTools.Method(abilityBarType, "SetUpgradeIndex",
                new[] { typeof(int), typeof(bool) });
            if (setUpgradeIndexMethod == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no SetUpgradeIndex method to patch. Hover flash not suppressed.");
            }
            else {
                harmony.Patch(setUpgradeIndexMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(DisableSkillsBarPatch), nameof(SetUpgradeIndexPrefix))));
            }

            // UpgradeAbility(int) is called directly from OnMouseClick with no UpgradeMode check,
            // so a click upgrades an ability even when the bar appears closed. Block it unless Alt is
            // held OR the bar is already intentionally open (UpgradeMode == true).
            var upgradeAbilityMethod = AccessTools.Method(abilityBarType, "UpgradeAbility",
                new[] { typeof(int) });
            if (upgradeAbilityMethod == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no UpgradeAbility(int) method to patch. Click gating disabled.");
            }
            else {
                harmony.Patch(upgradeAbilityMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(DisableSkillsBarPatch), nameof(UpgradeAbilityPrefix))));
            }

            DisableSkillsBarMod.PublicLogger.LogInfo($"Patched {abilityBarType.FullName}: hover and click gated behind Alt.");
        }

        // Block hover from setting UpgradeMode=true unless Alt is held.
        // Without this, SetUpgradeIndex(mouseHover: true) causes a flash + SFX before CheckInput resets it.
        public static bool SetUpgradeIndexPrefix(bool mouseHover) {
            return !mouseHover || IsInteractionUnlocked();
        }

        // Block direct mouse-click upgrades unless Alt is held OR the bar is already open.
        // OnMouseClick calls UpgradeAbility(int) directly, bypassing UpgradeMode entirely.
        public static bool UpgradeAbilityPrefix(object __instance) {
            if (IsInteractionUnlocked()) {
                return true;
            }

            var upgradeModeProp = __instance.GetType().GetProperty("UpgradeMode");
            return upgradeModeProp?.GetValue(__instance) is true;
        }

        // Suppress hover-based skill bar activation unless Alt is held.
        // Setting allowOpening=false causes AbilityBar.CheckInput to run UpgradeMode=false,
        // which prevents the DisableAttack input mode and cursor unlock triggered by hover.
        public static void CheckInputPrefix(ref bool allowOpening) {
            if (!IsInteractionUnlocked()) {
                allowOpening = false;
            }
        }

        private static bool IsInteractionUnlocked() {
            try {
                return UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt) ||
                       UnityEngine.Input.GetKey(UnityEngine.KeyCode.RightAlt);
            }
            catch (InvalidOperationException) {
                if (!_loggedLegacyInputUnavailable) {
                    _loggedLegacyInputUnavailable = true;
                    DisableSkillsBarMod.PublicLogger.LogInfo("Legacy UnityEngine.Input is disabled; using Unity Input System fallback for Alt detection.");
                }

                return IsAltPressedViaInputSystem();
            }
        }

        private static bool IsAltPressedViaInputSystem() {
            if (!EnsureInputSystemReflection()) {
                return false;
            }

            var keyboard = _keyboardCurrentProp.GetValue(null, null);
            if (keyboard == null) {
                return false;
            }

            return IsKeyPressed(_leftAltKeyProp.GetValue(keyboard, null)) ||
                   IsKeyPressed(_rightAltKeyProp.GetValue(keyboard, null));
        }

        private static bool IsKeyPressed(object keyControl) {
            return keyControl != null &&
                   _keyIsPressedProp != null &&
                   _keyIsPressedProp.GetValue(keyControl, null) is bool isPressed &&
                   isPressed;
        }

        private static bool EnsureInputSystemReflection() {
            if (_inputSystemInitAttempted) {
                return _inputSystemAvailable;
            }

            _inputSystemInitAttempted = true;

            var keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null) {
                return false;
            }

            _keyboardCurrentProp = keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _leftAltKeyProp = keyboardType.GetProperty("leftAltKey", BindingFlags.Public | BindingFlags.Instance);
            _rightAltKeyProp = keyboardType.GetProperty("rightAltKey", BindingFlags.Public | BindingFlags.Instance);

            var keyControlType = Type.GetType("UnityEngine.InputSystem.Controls.KeyControl, Unity.InputSystem");
            _keyIsPressedProp = keyControlType?.GetProperty("isPressed", BindingFlags.Public | BindingFlags.Instance);

            _inputSystemAvailable = _keyboardCurrentProp != null &&
                                    _leftAltKeyProp != null &&
                                    _rightAltKeyProp != null &&
                                    _keyIsPressedProp != null;

            if (!_inputSystemAvailable) {
                DisableSkillsBarMod.PublicLogger.LogWarning("Unity Input System fallback could not be initialized. Alt gating will remain locked when legacy input is unavailable.");
            }

            return _inputSystemAvailable;
        }
    }
}
