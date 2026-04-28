using HarmonyLib;
using RR.UI.Pages;
using UnityEngine.InputSystem;

namespace WildguardModFramework.PlayerManagement {
    internal static class PlayerManagementPatch {
        internal static void Apply(Harmony harmony) {
            var onInit = AccessTools.Method(typeof(BaseHUDPage), "OnInit");
            if (onInit == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BaseHUDPage.OnInit not found — player management overlay inactive.");
            } else {
                harmony.Patch(onInit, postfix: new HarmonyMethod(typeof(PlayerManagementPatch), nameof(OnInitPostfix)));
            }

            var onUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (onUpdate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BaseHUDPage.OnUpdate not found — F2 key inactive.");
            } else {
                harmony.Patch(onUpdate, postfix: new HarmonyMethod(typeof(PlayerManagementPatch), nameof(OnUpdatePostfix)));
            }

            var onDeactivate = AccessTools.Method(typeof(BaseHUDPage), "OnDeactivate");
            if (onDeactivate != null) {
                harmony.Patch(onDeactivate, postfix: new HarmonyMethod(typeof(PlayerManagementPatch), nameof(OnDeactivatePostfix)));
            }
        }

        private static void OnInitPostfix(BaseHUDPage __instance) {
            if (__instance is GameHUDPage || __instance is LobbyHUDPage) {
                PlayerManagementController.OnHudInit(__instance);
            }
        }

        private static string _lastPageType;

        private static void OnUpdatePostfix(BaseHUDPage __instance) {
            if (!(__instance is GameHUDPage) && !(__instance is LobbyHUDPage)) { return; }
            var typeName = __instance.GetType().Name;
            if (typeName != _lastPageType) {
                _lastPageType = typeName;
                WmfMod.PublicLogger.LogInfo($"[PMgmt] OnUpdate page type: {typeName}");
            }
            PlayerManagementController.SetActivePage(__instance);
            var f2 = Keyboard.current?[Key.F2];
            if (f2?.wasPressedThisFrame == true) {
                PlayerManagementController.ShowOverlay();
            } else if (f2?.wasReleasedThisFrame == true) {
                PlayerManagementController.HideOverlay();
            }
        }

        private static void OnDeactivatePostfix(BaseHUDPage __instance) {
            if (__instance is GameHUDPage || __instance is LobbyHUDPage) {
                PlayerManagementController.ForceClose(__instance);
            }
        }
    }
}
