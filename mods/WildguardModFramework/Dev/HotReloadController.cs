#if DEV_HOTRELOAD
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using RR.UI.Pages;
using UnityEngine.InputSystem;
using UnityHotReloadNS;

namespace WildguardModFramework.Dev {
    internal static class HotReloadController {
        private static string _dllPath;
        private static Harmony _harmony;
        private static MethodInfo _hudUpdate;

        internal static void Initialize(Harmony harmony, string dllPath) {
            _harmony = harmony;
            _dllPath = dllPath;

            _hudUpdate = AccessTools.Method(typeof(BaseHUDPage), "Update");
            if (_hudUpdate == null) {
                WmfMod.PublicLogger.LogWarning("[HotReload] BaseHUDPage.Update not found — F9 unavailable.");
            }
        }

        internal static void Enable() {
            if (_hudUpdate == null) { return; }
            _harmony.Patch(_hudUpdate, postfix: new HarmonyMethod(typeof(HotReloadController), nameof(HudUpdatePostfix)));
            WmfMod.PublicLogger.LogInfo("[HotReload] Hooked BaseHUDPage.Update. Press F9 to reload.");
        }

        internal static void Disable() {
            _harmony.UnpatchSelf();
        }

        private static void HudUpdatePostfix() {
            if (Keyboard.current == null || !Keyboard.current[Key.F9].wasPressedThisFrame) {
                return;
            }
            WmfMod.PublicLogger.LogInfo("[HotReload] F9 pressed.");
            if (string.IsNullOrEmpty(_dllPath) || !File.Exists(_dllPath)) {
                WmfMod.PublicLogger.LogWarning($"[HotReload] DLL not found: {_dllPath}");
                return;
            }
            WmfMod.PublicLogger.LogInfo("[HotReload] Reloading...");
            try {
                UnityHotReload.LoadNewAssemblyVersion(Assembly.GetExecutingAssembly(), _dllPath);
                WmfMod.PublicLogger.LogInfo("[HotReload] Done.");
            }
            catch (Exception ex) {
                WmfMod.PublicLogger.LogError($"[HotReload] Failed: {ex}");
            }
        }
    }
}
#endif
