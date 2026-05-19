using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RR;
using RR.Game.Pickups;
using RR.Input;
using RR.Level;
using RR.UI.Pages;
using RR.UI.UISystem;
using ThePit.UI;

namespace ThePit.Patch {
    // When The Pit (Beta) is active, intercepts the planning table interaction in the lobby
    // and shows the match config overlay instead of the normal raid-planning flow.
    // The overlay's OK button triggers Handle_GameEventRaidSelected on the LobbyManager
    // (via reflection), which sets IsRaidReadyToGo=true and starts the timer that fires
    // the cutscene RPC — skipping the raid-selection UI.
    internal static class PlanningTablePatch {
        private static readonly ConditionalWeakTable<LobbyHUDPage, HostConfigOverlay> _overlays = new();
        private static MethodInfo _handleRaidSelectedMethod;

        internal static void Apply(Harmony harmony) {
            _handleRaidSelectedMethod = AccessTools.Method(typeof(LobbyManager), "Handle_GameEventRaidSelected");
            if (_handleRaidSelectedMethod == null) {
                ThePitMod.PublicLogger.LogWarning(
                    "ThePit: LobbyManager.Handle_GameEventRaidSelected not found — lobby config overlay will not start raid.");
            }

            var onCardCollected = AccessTools.Method(typeof(PlanningTablePickup), nameof(PlanningTablePickup.OnCardCollected));
            if (onCardCollected == null) {
                ThePitMod.PublicLogger.LogWarning(
                    "ThePit: PlanningTablePickup.OnCardCollected not found — lobby config overlay inactive.");
                return;
            }
            harmony.Patch(onCardCollected,
                prefix: new HarmonyMethod(typeof(PlanningTablePatch), nameof(OnCardCollectedPrefix)));

            var onNav = AccessTools.Method(typeof(LobbyHUDPage), nameof(LobbyHUDPage.OnNavigateInput));
            if (onNav != null) {
                harmony.Patch(onNav,
                    prefix: new HarmonyMethod(typeof(PlanningTablePatch), nameof(OnNavigateInputPrefix)));
            }
        }

        // Replaces the normal RaidPage.Open() with our config overlay for the host.
        private static bool OnCardCollectedPrefix(PlanningTablePickup __instance) {
            if (!ThePitState.IsDraftMode) { return true; }
            if (!__instance.Runner.IsServer) { return false; }
            if (PlayerManager.Instance.LocalPlayer?.PlayableChampion == null) { return false; }

            var hudPage = UIManager.Instance.GetHUDPage("LobbyHUDPage") as LobbyHUDPage;
            if (hudPage == null) { return false; }

            _overlays.GetValue(hudPage, p => new HostConfigOverlay(p.RootElement, () => {
                DifficultyManager.Instance.Difficulty = Difficulty.Normal;
                DifficultyManager.Instance.DangerRisky = 0;
                var lobbyManager = GameManager.Instance.GetLobbyManager();
                if (lobbyManager != null) {
                    _handleRaidSelectedMethod?.Invoke(lobbyManager, null);
                }
            })).Show();

            return false;
        }

        // Block lobby HUD navigation while the overlay is visible.
        // On Cancel (ESC), close the overlay instead of propagating.
        private static bool OnNavigateInputPrefix(LobbyHUDPage __instance, InputPressEvent evt) {
            if (!_overlays.TryGetValue(__instance, out var overlay) || !overlay.IsVisible) {
                return true;
            }
            if (evt.IsPressed && evt.Type == PageNavType.Cancel) {
                overlay.Close();
            }
            return false;
        }
    }
}
