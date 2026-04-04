using BeginnersWelcome.UI;
using HarmonyLib;
using RR.Game.Damage;
using RR.Game.Stats;
using RR.UI.Pages;

namespace BeginnersWelcome.Patch {
    public static class BeginnersWelcomePatch {
        public static void Apply(Harmony harmony) {
            var onInit = AccessTools.Method(typeof(BaseHUDPage), "OnInit");
            var onUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            var onDeactivate = AccessTools.Method(typeof(BaseHUDPage), "OnDeactivate");
            if (onInit == null || onUpdate == null || onDeactivate == null) {
                BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find BaseHUDPage methods — panel patch inactive.");
            } else {
                harmony.Patch(onInit, postfix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(OnHUDInitPostfix))));
                harmony.Patch(onUpdate, postfix: new HarmonyMethod(AccessTools.Method(typeof(BeginnersWelcomePatch), nameof(OnHUDUpdatePostfix))));
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

        private static void OnHUDInitPostfix(BaseHUDPage __instance) => HandicapDisplay.OnPageInit(__instance);
        private static void OnHUDUpdatePostfix(BaseHUDPage __instance) => HandicapDisplay.OnPageUpdate(__instance);
        private static void OnHUDDeactivatePostfix(BaseHUDPage __instance) => HandicapDisplay.OnPageDeactivate(__instance);

        private static bool TakeBasicDamagePrefix(Health __instance, ref DamageDescriptor dmgDesc)
            => HandicapManager.OnTakeBasicDamage(__instance, ref dmgDesc);

        private static bool TakeDOTDamagePrefix(Health __instance, ref float damage)
            => HandicapManager.OnTakeDOTDamage(__instance, ref damage);
    }
}
