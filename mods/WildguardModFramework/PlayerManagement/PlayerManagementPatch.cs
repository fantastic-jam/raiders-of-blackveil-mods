using HarmonyLib;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine.InputSystem;

namespace WildguardModFramework.PlayerManagement {
    internal static class PlayerManagementPatch {
        internal static void Apply(Harmony harmony) {
            // Pages are built in their PageArgs constructor, but IL2CPP constructors can't be patched.
            // Build the overlay on the first OnActivate of each concrete HUD (after the HUD's own elements → on top).
            var activatePrefix = new HarmonyMethod(typeof(PlayerManagementPatch), nameof(HudActivatePrefix));
            var gameHudActivate = AccessTools.Method(typeof(GameHUDPage), "OnActivate");
            if (gameHudActivate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: GameHUDPage.OnActivate not found — in-raid player management overlay inactive.");
            } else {
                harmony.Patch(gameHudActivate, prefix: activatePrefix);
            }
            var lobbyHudActivate = AccessTools.Method(typeof(LobbyHUDPage), "OnActivate");
            if (lobbyHudActivate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: LobbyHUDPage.OnActivate not found — lobby player management overlay inactive.");
            } else {
                harmony.Patch(lobbyHudActivate, prefix: activatePrefix);
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

        private static void HudActivatePrefix(BaseHUDPage __instance) => PlayerManagementController.OnHudInit(__instance);

        // IL2CPP: __instance is wrapped as BaseHUDPage — TryCast tests the native runtime type.
        private static void OnUpdatePostfix(BaseHUDPage __instance) {
            if (__instance.TryCast<GameHUDPage>() == null && __instance.TryCast<LobbyHUDPage>() == null) { return; }
            PlayerManagementController.SetActivePage(__instance);
            var kb = Keyboard.current;
            var listKey = kb?[WmfConfig.PlayerListKey.Value];
            if (listKey?.wasPressedThisFrame == true) {
                PlayerManagementController.ShowOverlay();
            } else if (listKey?.wasReleasedThisFrame == true) {
                PlayerManagementController.HideOverlay();
            }
            PlayerManagementController.TickInputMode();
        }

        private static void OnDeactivatePostfix(BaseHUDPage __instance) {
            if (__instance.TryCast<GameHUDPage>() != null || __instance.TryCast<LobbyHUDPage>() != null) {
                PlayerManagementController.ForceClose(__instance);
            }
        }
    }
}
