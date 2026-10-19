using CheatManager.UI;
using Fusion;
using HarmonyLib;
using RR;
using RR.Config;
using RR.Scripts.UI.Extensions;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine.UIElements;

namespace CheatManager.Patch {
    public static class CheatManagerPatch {
        internal static bool Disabled { get; private set; }
        private static VisualElement _debugButtons;

        internal static void SetDisabled() {
            Disabled = true;
            _debugButtons?.VisibleDisplay(visible: false);
        }
        internal static void SetEnabled() => Disabled = false;

        private static bool IsAuthorized() {
            var nm = NetworkManager.Instance;
            if (nm == null) { return false; }
            // Solo mode: local runner is the only authority — always allow.
            if (nm.FusionGameMode == GameMode.Single) { return true; }
            // Multiplayer: allow only on the host/server.
            return nm.NetworkRunner != null && nm.NetworkRunner.IsServer;
        }

        public static void Apply(Harmony harmony) {
            var getter = AccessTools.PropertyGetter(typeof(LocalSettings), nameof(LocalSettings.Dev_EnableCheatHotkeys));
            if (getter == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find LocalSettings.Dev_EnableCheatHotkeys getter — patch inactive.");
            } else {
                harmony.Patch(getter, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(EnableCheatHotkeysPostfix))));
            }

            // PageBase.OnInit was removed; pages now build their UI in the constructor.
            var hudCtor = AccessTools.Constructor(typeof(BaseHUDPage), new[] { typeof(string), typeof(PageArgs) });
            if (hudCtor == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find BaseHUDPage(string, PageArgs) ctor — hotkeys display inactive.");
            } else {
                harmony.Patch(hudCtor, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(OnHUDCtorPostfix))));
            }

            var onUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (onUpdate == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find BaseHUDPage.OnUpdate — hotkeys display inactive.");
            } else {
                harmony.Patch(onUpdate, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(OnHUDUpdatePostfix))));
            }

            var lobbyCtor = AccessTools.Constructor(typeof(LobbyHUDPage), new[] { typeof(PageArgs) });
            if (lobbyCtor == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find LobbyHUDPage(PageArgs) ctor — DebugButtons hide on disable inactive.");
            } else {
                harmony.Patch(lobbyCtor, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(OnLobbyCtorPostfix))));
            }

            CheatManagerMod.PublicLogger.LogInfo("CheatManager patch applied.");
        }

        private static void EnableCheatHotkeysPostfix(ref bool __result) {
            if (Disabled) { return; }
            if (IsAuthorized()) { __result = true; }
        }

        private static void OnLobbyCtorPostfix(LobbyHUDPage __instance) {
            _debugButtons = __instance.RootElement?.Q("DebugButtons");
        }

        private static void OnHUDCtorPostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            HotkeyDisplay.OnPageInit(__instance);
        }

        private static void OnHUDUpdatePostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            if (IsAuthorized()) { HotkeyDisplay.OnPageUpdate(__instance); }
        }
    }
}
