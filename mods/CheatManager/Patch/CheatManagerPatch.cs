using HarmonyLib;
using RR.Config;

namespace CheatManager.Patch {
    public static class CheatManagerPatch {
        public static void Apply(Harmony harmony) {
            var getter = AccessTools.PropertyGetter(typeof(PlayerSettings), nameof(PlayerSettings.Dev_EnableCheatHotkeys));
            if (getter == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find PlayerSettings.Dev_EnableCheatHotkeys getter — patch inactive.");
                return;
            }

            harmony.Patch(getter,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(EnableCheatHotkeysPostfix))));

            CheatManagerMod.PublicLogger.LogInfo("CheatManager patch applied.");
        }

        static void EnableCheatHotkeysPostfix(ref bool __result) {
            __result = true;
        }
    }
}
