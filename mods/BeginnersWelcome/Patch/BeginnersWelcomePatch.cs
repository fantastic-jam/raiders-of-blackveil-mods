using BeginnersWelcome.UI;
using HarmonyLib;
using RR.Game.Damage;
using RR.Game.Stats;
using RR.UI.Pages;
using RR.UI.UISystem;

namespace BeginnersWelcome.Patch {
    public static class BeginnersWelcomePatch {
        internal static bool Disabled { get; private set; }
        internal static void SetDisabled() => Disabled = true;
        internal static void SetEnabled() => Disabled = false;

        public static void Apply(Harmony harmony) {
            // PageBase.OnInit was removed; pages now build their UI in the constructor.
            var hudCtor = AccessTools.Constructor(typeof(BaseHUDPage), new[] { typeof(string), typeof(PageArgs) });
            if (hudCtor == null) {
                BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find BaseHUDPage(string, PageArgs) ctor — panel inactive.");
            } else {
                harmony.Patch(hudCtor, postfix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(OnHUDCtorPostfix))));
            }

            var onUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (onUpdate == null) {
                BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find BaseHUDPage.OnUpdate — panel toggle key inactive.");
            } else {
                harmony.Patch(onUpdate, postfix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(OnHUDUpdatePostfix))));
            }

            var onDeactivate = AccessTools.Method(typeof(BaseHUDPage), "OnDeactivate");
            if (onDeactivate == null) {
                BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find BaseHUDPage.OnDeactivate — panel auto-hide inactive.");
            } else {
                harmony.Patch(onDeactivate, postfix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(OnHUDDeactivatePostfix))));
            }

            if (HandicapManager.Init()) {
                var takeBasicDamage = AccessTools.Method(typeof(Health), "TakeBasicDamage");
                if (takeBasicDamage == null) {
                    BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find Health.TakeBasicDamage — damage patch inactive.");
                } else {
                    harmony.Patch(takeBasicDamage, prefix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(TakeBasicDamagePrefix))));
                }

                var takeDOTDamage = AccessTools.Method(typeof(Health), "TakeDOTDamage");
                if (takeDOTDamage == null) {
                    BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find Health.TakeDOTDamage — DOT patch inactive.");
                } else {
                    harmony.Patch(takeDOTDamage, prefix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(TakeDOTDamagePrefix))));
                }
            }

            BeginnersWelcomeMod.PublicLogger.LogInfo("BeginnersWelcome patch applied.");
        }

        private static void OnHUDCtorPostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            HandicapDisplay.OnPageInit(__instance);
        }

        private static void OnHUDUpdatePostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            HandicapDisplay.OnPageUpdate(__instance);
        }

        private static void OnHUDDeactivatePostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            HandicapDisplay.OnPageDeactivate(__instance);
        }

        private static bool TakeBasicDamagePrefix(Health __instance, ref DamageDescriptor dmgDesc) {
            if (Disabled) { return true; }
            return HandicapManager.OnTakeBasicDamage(__instance, ref dmgDesc);
        }

        private static bool TakeDOTDamagePrefix(Health __instance, ref float damage) {
            if (Disabled) { return true; }
            return HandicapManager.OnTakeDOTDamage(__instance, ref damage);
        }
    }
}
