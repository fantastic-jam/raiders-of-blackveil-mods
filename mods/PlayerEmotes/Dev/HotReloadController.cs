#if DEV_HOTRELOAD
using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using RR.UI.UISystem;
using UnityEngine.InputSystem;
using UnityHotReloadNS;

namespace PlayerEmotes.Dev {
    internal static class HotReloadController {
        private static string _dllPath;
        private static Harmony _harmony;
        private static MethodInfo _hudUpdate;

        internal static void Initialize(Harmony harmony, string dllPath) {
            _harmony = harmony;
            _dllPath = dllPath;

            _hudUpdate = AccessTools.Method(typeof(PageBase), nameof(PageBase.OnUpdateBase));
            if (_hudUpdate == null) {
                PlayerEmotesMod.PublicLogger.LogWarning("[HotReload] PageBase.OnUpdateBase not found — F9 unavailable.");
            }
        }

        internal static void Enable() {
            if (_hudUpdate == null) { return; }
            _harmony.Patch(_hudUpdate, postfix: new HarmonyMethod(typeof(HotReloadController), nameof(HudUpdatePostfix)));
            PlayerEmotesMod.PublicLogger.LogInfo("[HotReload] Hooked PageBase.OnUpdateBase. Press F9 to reload.");
        }

        internal static void Disable() {
            _harmony.UnpatchSelf();
        }

        private static void HudUpdatePostfix() {
            if (Keyboard.current == null || !Keyboard.current[Key.F9].wasPressedThisFrame) {
                return;
            }
            PlayerEmotesMod.PublicLogger.LogInfo("[HotReload] F9 pressed.");
            if (string.IsNullOrEmpty(_dllPath) || !File.Exists(_dllPath)) {
                PlayerEmotesMod.PublicLogger.LogWarning($"[HotReload] DLL not found: {_dllPath}");
                return;
            }
            PlayerEmotesMod.PublicLogger.LogInfo("[HotReload] Reloading...");
            try {
                UnityHotReload.LoadNewAssemblyVersion(Assembly.GetExecutingAssembly(), _dllPath);
                PlayerEmotesMod.PublicLogger.LogInfo("[HotReload] Done.");
            }
            catch (Exception ex) {
                PlayerEmotesMod.PublicLogger.LogError($"[HotReload] Failed: {ex}");
            }
        }
    }
}
#endif
