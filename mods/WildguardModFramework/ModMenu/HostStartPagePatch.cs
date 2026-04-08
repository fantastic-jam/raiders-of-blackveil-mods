using HarmonyLib;
using WildguardModFramework.Registry;
using RR;
using RR.UI.Pages;

namespace WildguardModFramework.ModMenu {
    internal static class HostStartPagePatch {
        internal static void Apply(Harmony harmony) {
            var onActivate = AccessTools.Method(typeof(MenuStartHostPage), "OnActivate");
            if (onActivate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuStartHostPage.OnActivate not found — host page UI patch inactive.");
            } else {
                harmony.Patch(onActivate, postfix: new HarmonyMethod(typeof(HostStartPagePatch), nameof(OnActivatePostfix)));
            }

            var beginSession = AccessTools.Method(typeof(BackendManager), nameof(BackendManager.BeginPlaySession));
            if (beginSession == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BackendManager.BeginPlaySession not found — session tag and disable patch inactive.");
            } else {
                harmony.Patch(beginSession, prefix: new HarmonyMethod(typeof(HostStartPagePatch), nameof(BeginPlaySessionPrefix)));
            }
        }

        private static void OnActivatePostfix(MenuStartHostPage __instance) {
            ModScanner.Scan();
            HostPageController.Activate(__instance);
        }

        private static void BeginPlaySessionPrefix(ref string sessionTag, BackendManager.PlaySessionMode playSessionMode) =>
            SessionOrchestrator.Begin(ref sessionTag,
                HostPageController.Current?.AllowMods ?? true,
                HostPageController.Current?.AllowCheats ?? true,
                playSessionMode);
    }
}
