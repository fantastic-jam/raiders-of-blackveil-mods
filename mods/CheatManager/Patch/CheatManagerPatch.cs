using CheatManager.UI;
using HarmonyLib;
using RR.Config;
using RR.UI.Pages;

namespace CheatManager.Patch {
    public static class CheatManagerPatch {
        public static void Apply(Harmony harmony) {
            var getter = AccessTools.PropertyGetter(typeof(PlayerSettings), nameof(PlayerSettings.Dev_EnableCheatHotkeys));
            if (getter == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find PlayerSettings.Dev_EnableCheatHotkeys getter — patch inactive.");
            } else {
                harmony.Patch(getter, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(EnableCheatHotkeysPostfix))));
            }

            var onInit = AccessTools.Method(typeof(BaseHUDPage), "OnInit");
            var onUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (onInit == null || onUpdate == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find BaseHUDPage.OnInit/OnUpdate — hotkeys display patch inactive.");
            } else {
                harmony.Patch(onInit, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(OnHUDInitPostfix))));
                harmony.Patch(onUpdate, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(OnHUDUpdatePostfix))));
            }

            CheatManagerMod.PublicLogger.LogInfo("CheatManager patch applied.");
        }

        private static void EnableCheatHotkeysPostfix(ref bool __result) => __result = true;
        private static void OnHUDInitPostfix(BaseHUDPage __instance) => HotkeyDisplay.OnPageInit(__instance);
        private static void OnHUDUpdatePostfix(BaseHUDPage __instance) => HotkeyDisplay.OnPageUpdate(__instance);
    }
}
