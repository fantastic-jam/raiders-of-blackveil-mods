using CheatManager.UI;
using Fusion;
using HarmonyLib;
using RR;
using RR.Config;
using RR.Scripts.UI.Extensions;
using RR.UI.Pages;
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

            var lobbyInit = AccessTools.Method(typeof(LobbyHUDPage), "OnInit");
            if (lobbyInit == null) {
                CheatManagerMod.PublicLogger.LogWarning("CheatManager: Could not find LobbyHUDPage.OnInit — DebugButtons hide on disable inactive.");
            } else {
                harmony.Patch(lobbyInit, postfix: new HarmonyMethod(AccessTools.Method(typeof(CheatManagerPatch), nameof(OnLobbyInitPostfix))));
            }

            CheatManagerMod.PublicLogger.LogInfo("CheatManager patch applied.");
        }

        private static void EnableCheatHotkeysPostfix(ref bool __result) {
            if (Disabled) { return; }
            if (IsAuthorized()) { __result = true; }
        }

        private static void OnLobbyInitPostfix(LobbyHUDPage __instance) {
            _debugButtons = __instance.RootElement?.Q("DebugButtons");
        }

        private static void OnHUDInitPostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            HotkeyDisplay.OnPageInit(__instance);
        }

        private static void OnHUDUpdatePostfix(BaseHUDPage __instance) {
            if (Disabled) { return; }
            if (IsAuthorized()) { HotkeyDisplay.OnPageUpdate(__instance); }
        }
    }
}
