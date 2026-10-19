using HarmonyLib;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine.InputSystem;

namespace WildguardModFramework.PlayerManagement {
    internal static class PlayerManagementPatch {
        internal static void Apply(Harmony harmony) {
            // Pages no longer have an OnInit hook — all init happens in the PageArgs constructor.
            // Patch the concrete HUD ctors so the overlay is added after the HUD's own elements (on top).
            var ctorPostfix = new HarmonyMethod(typeof(PlayerManagementPatch), nameof(HudCtorPostfix));
            var gameHudCtor = AccessTools.Constructor(typeof(GameHUDPage), new[] { typeof(PageArgs) });
            if (gameHudCtor == null) {
                WmfMod.PublicLogger.LogWarning("WMF: GameHUDPage(PageArgs) constructor not found — in-raid player management overlay inactive.");
            } else {
                harmony.Patch(gameHudCtor, postfix: ctorPostfix);
            }
            var lobbyHudCtor = AccessTools.Constructor(typeof(LobbyHUDPage), new[] { typeof(PageArgs) });
            if (lobbyHudCtor == null) {
                WmfMod.PublicLogger.LogWarning("WMF: LobbyHUDPage(PageArgs) constructor not found — lobby player management overlay inactive.");
            } else {
                harmony.Patch(lobbyHudCtor, postfix: ctorPostfix);
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

        private static void HudCtorPostfix(BaseHUDPage __instance) => PlayerManagementController.OnHudInit(__instance);

        private static void OnUpdatePostfix(BaseHUDPage __instance) {
            if (!(__instance is GameHUDPage) && !(__instance is LobbyHUDPage)) { return; }
            PlayerManagementController.SetActivePage(__instance);
            var kb = Keyboard.current;
            var listKey = kb?[WmfConfig.PlayerListKey.Value];
            if (listKey?.wasPressedThisFrame == true) {
                PlayerManagementController.ShowOverlay();
            } else if (listKey?.wasReleasedThisFrame == true) {
                PlayerManagementController.HideOverlay();
            }
#if WMF_DEBUG
            if (kb?[Key.F5].wasPressedThisFrame == true) {
                WildguardModFramework.Debug.GameModeDebug.PollSessions();
            }
#endif
            PlayerManagementController.TickInputMode();
        }

        private static void OnDeactivatePostfix(BaseHUDPage __instance) {
            if (__instance is GameHUDPage || __instance is LobbyHUDPage) {
                PlayerManagementController.ForceClose(__instance);
            }
        }
    }
}
