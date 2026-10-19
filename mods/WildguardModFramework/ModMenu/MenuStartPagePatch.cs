using HarmonyLib;
using WildguardModFramework.Lifecycle;
using WildguardModFramework.Registry;
using RR;
using RR.Input;
using RR.UI.Pages;
using RR.UI.UISystem;

namespace WildguardModFramework.ModMenu {
    internal static class MenuStartPagePatch {
        internal static void Apply(Harmony harmony) {
            var appManagerInit = AccessTools.Method(typeof(AppManager), nameof(AppManager.Init));
            if (appManagerInit != null) {
                harmony.Patch(appManagerInit, prefix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(AppManagerInitPrefix)));
            } else {
                WmfMod.PublicLogger.LogWarning("WMF: AppManager.Init not found — startup disable will not run.");
            }

            // Pages no longer have an OnInit hook — all init happens in the PageArgs constructor.
            var ctor = AccessTools.Constructor(typeof(MenuStartPage), new[] { typeof(PageArgs) });
            if (ctor == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuStartPage(PageArgs) constructor not found — Mods button skipped.");
                return;
            }
            harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(CtorPostfix)));

            // ResetCursor clears and rebuilds the button cursor on every OnActivate / OnReady.
            var resetCursor = AccessTools.Method(typeof(MenuStartPage), "ResetCursor");
            if (resetCursor == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuStartPage.ResetCursor not found — Mods button keyboard/gamepad nav unavailable.");
            } else {
                harmony.Patch(resetCursor, postfix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(ResetCursorPostfix)));
            }

            var onActivate = AccessTools.Method(typeof(MenuStartPage), "OnActivate");
            if (onActivate != null) {
                harmony.Patch(onActivate, postfix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(OnActivatePostfix)));
            }

            var onNavigate = AccessTools.Method(typeof(MenuStartPage), "OnNavigateInput");
            if (onNavigate != null) {
                harmony.Patch(onNavigate, prefix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(OnNavigateInputPrefix)));
            }
        }

        private static void AppManagerInitPrefix() {
            ModScanner.Scan();
            WmfConfig.Sync(ModScanner.AllMods());
            ModLifecycle.ApplyStartupDisables();
        }

        private static void CtorPostfix(MenuStartPage __instance) {
            ModsButtonInjector.Inject(__instance);
            SoloModePickerInjector.Inject(__instance);
        }

        private static void ResetCursorPostfix(MenuStartPage __instance) => ModsButtonInjector.RegisterInCursor(__instance);

        private static void OnActivatePostfix() {
            // Re-scan so WmfOptions tab names resolve with the now-loaded language,
            // then reset the overlay so it rebuilds fresh on next open.
            ModScanner.Scan();
            ModsButtonInjector.Reset();
            ModLifecycle.DisableAllGameModes();
        }

        private static bool OnNavigateInputPrefix(InputPressEvent evt) =>
            !SoloModePickerInjector.HandleInput(evt) && !ModsButtonInjector.HandleInput(evt);
    }
}
