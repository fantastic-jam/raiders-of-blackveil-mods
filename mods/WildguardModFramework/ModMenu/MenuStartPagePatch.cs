using HarmonyLib;
using WildguardModFramework.Lifecycle;
using WildguardModFramework.Registry;
using RR;
using RR.Input;
using RR.UI.Pages;

namespace WildguardModFramework.ModMenu {
    internal static class MenuStartPagePatch {
        internal static void Apply(Harmony harmony) {
            var appManagerInit = AccessTools.Method(typeof(AppManager), nameof(AppManager.Init));
            if (appManagerInit != null) {
                harmony.Patch(appManagerInit, prefix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(AppManagerInitPrefix)));
            } else {
                WmfMod.PublicLogger.LogWarning("WMF: AppManager.Init not found — startup disable will not run.");
            }

            var onInit = AccessTools.Method(typeof(MenuStartPage), "OnInit");
            if (onInit == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuStartPage.OnInit not found — Mods button skipped.");
                return;
            }
            harmony.Patch(onInit, postfix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(OnInitPostfix)));

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

        private static void OnInitPostfix(MenuStartPage __instance) {
            ModsButtonInjector.Inject(__instance);
            SoloModePickerInjector.Inject(__instance);
        }

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
