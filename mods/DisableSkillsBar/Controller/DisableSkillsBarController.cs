using System;
using System.Reflection;

namespace DisableSkillsBar.Controller {
    internal static class DisableSkillsBarController {
        // Enable/disable toggle surfaced to the WMF duck-typing interface
        internal static bool Disabled { get; private set; }
        internal static void SetDisabled() => Disabled = true;
        internal static void SetEnabled() => Disabled = false;

        // Resolved once via SetUpgradeModeProp() — called from Apply()
        internal static PropertyInfo UpgradeModeProp { get; private set; }

        internal static void SetUpgradeModeProp(PropertyInfo prop) => UpgradeModeProp = prop;

        // Input System lazy-init state
        private static bool _loggedLegacyInputUnavailable;
        private static bool _inputSystemInitAttempted;
        private static bool _inputSystemAvailable;
        private static PropertyInfo _keyboardCurrentProp;
        private static PropertyInfo _leftAltKeyProp;
        private static PropertyInfo _rightAltKeyProp;
        private static PropertyInfo _keyIsPressedProp;

        // Called from SetUpgradeIndexPrefix — block mouseHover unless Alt held
        internal static bool ShouldAllowSetUpgradeIndex(bool mouseHover) =>
            Disabled || !mouseHover || IsInteractionUnlocked();

        // Called from UpgradeAbilityPrefix — block click unless Alt held or bar already open
        internal static bool ShouldAllowUpgradeAbility(object instance) {
            if (Disabled) {
                return true;
            }

            if (IsInteractionUnlocked()) {
                return true;
            }

            return UpgradeModeProp?.GetValue(instance) is true;
        }

        // Called from CheckInputPrefix — suppress bar opening; resets UpgradeMode when Alt not held.
        // Returns false (skip original) when gating is active, true (run original) otherwise.
        internal static bool CheckInput(object instance) {
            if (Disabled) {
                return true;
            }

            if (IsInteractionUnlocked()) {
                return true;
            }

            UpgradeModeProp?.SetValue(instance, false);
            return false;
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
