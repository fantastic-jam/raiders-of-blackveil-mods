#if DEV_HOTRELOAD
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using RR.UI.Pages;
using UnityEngine.InputSystem;
using UnityHotReloadNS;

namespace GhoulagUrskov.Dev {
    internal static class HotReloadController {
        private static string _dllPath;
        private static Harmony _harmony;
        private static MethodInfo _hudUpdate;

        internal static void Initialize(Harmony harmony, string dllPath) {
            _harmony = harmony;
            _dllPath = dllPath;

            _hudUpdate = AccessTools.Method(typeof(BaseHUDPage), "Update");
            if (_hudUpdate == null) {
                GhoulagUrskovMod.PublicLogger.LogWarning("[HotReload] BaseHUDPage.Update not found — F9 unavailable.");
            }
        }

        internal static void Enable() {
            if (_hudUpdate == null) { return; }
            _harmony.Patch(_hudUpdate, postfix: new HarmonyMethod(typeof(HotReloadController), nameof(HudUpdatePostfix)));
            GhoulagUrskovMod.PublicLogger.LogInfo("[HotReload] Hooked BaseHUDPage.Update. Press F9 to reload.");
        }

        internal static void Disable() {
            _harmony.UnpatchSelf();
        }

        private static void HudUpdatePostfix() {
            if (Keyboard.current == null || !Keyboard.current[Key.F9].wasPressedThisFrame) {
                return;
            }
            GhoulagUrskovMod.PublicLogger.LogInfo("[HotReload] F9 pressed.");
            if (string.IsNullOrEmpty(_dllPath) || !File.Exists(_dllPath)) {
                GhoulagUrskovMod.PublicLogger.LogWarning($"[HotReload] DLL not found: {_dllPath}");
                return;
            }
            GhoulagUrskovMod.PublicLogger.LogInfo("[HotReload] Reloading...");
            try {
                UnityHotReload.LoadNewAssemblyVersion(Assembly.GetExecutingAssembly(), _dllPath);
                GhoulagUrskovMod.PublicLogger.LogInfo("[HotReload] Done.");
            }
            catch (Exception ex) {
                GhoulagUrskovMod.PublicLogger.LogError($"[HotReload] Failed: {ex}");
            }
        }
    }
}
#endif
