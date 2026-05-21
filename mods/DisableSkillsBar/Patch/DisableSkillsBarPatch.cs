using DisableSkillsBar.Controller;
using HarmonyLib;

namespace DisableSkillsBar.Patch {
    public static class DisableSkillsBarPatch {
        internal static bool Disabled => DisableSkillsBarController.Disabled;
        internal static void SetDisabled() => DisableSkillsBarController.SetDisabled();
        internal static void SetEnabled() => DisableSkillsBarController.SetEnabled();

        public static void Apply(Harmony harmony) {
            var abilityBarType = AccessTools.TypeByName("RR.UI.Controls.HUD.AbilityBar");
            if (abilityBarType == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning("Could not find RR.UI.Controls.HUD.AbilityBar in current game build. Patch is inactive.");
                return;
            }

            var upgradeModeGetter = AccessTools.PropertyGetter(abilityBarType, "UpgradeMode");
            if (upgradeModeGetter == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no UpgradeMode getter — hover/click gating will not check bar state.");
            } else {
                DisableSkillsBarController.SetUpgradeModeProp(abilityBarType.GetProperty("UpgradeMode"));
            }

            var checkInputMethod = AccessTools.Method(abilityBarType, "CheckInput");
            if (checkInputMethod == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no CheckInput method to patch.");
                return;
            }
            harmony.Patch(checkInputMethod,
                prefix: new HarmonyMethod(typeof(DisableSkillsBarPatch), nameof(CheckInputPrefix)));

            var setUpgradeIndexMethod = AccessTools.Method(abilityBarType, "SetUpgradeIndex",
                new[] { typeof(int), typeof(bool) });
            if (setUpgradeIndexMethod == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no SetUpgradeIndex method to patch. Hover flash not suppressed.");
            } else {
                harmony.Patch(setUpgradeIndexMethod,
                    prefix: new HarmonyMethod(typeof(DisableSkillsBarPatch), nameof(SetUpgradeIndexPrefix)));
            }

            var upgradeAbilityMethod = AccessTools.Method(abilityBarType, "UpgradeAbility",
                new[] { typeof(int) });
            if (upgradeAbilityMethod == null) {
                DisableSkillsBarMod.PublicLogger.LogWarning($"Found {abilityBarType.FullName} but no UpgradeAbility(int) method to patch. Click gating disabled.");
            } else {
                harmony.Patch(upgradeAbilityMethod,
                    prefix: new HarmonyMethod(typeof(DisableSkillsBarPatch), nameof(UpgradeAbilityPrefix)));
            }

            DisableSkillsBarMod.PublicLogger.LogInfo($"Patched {abilityBarType.FullName}: hover and click gated behind Alt.");
        }

        private static bool SetUpgradeIndexPrefix(bool mouseHover) =>
            DisableSkillsBarController.ShouldAllowSetUpgradeIndex(mouseHover);

        private static bool UpgradeAbilityPrefix(object __instance) =>
            DisableSkillsBarController.ShouldAllowUpgradeAbility(__instance);

        private static bool CheckInputPrefix(object __instance) =>
            DisableSkillsBarController.CheckInput(__instance);
    }
}
