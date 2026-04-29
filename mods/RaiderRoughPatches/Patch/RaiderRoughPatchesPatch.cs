using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using RR.Level;
using RR.UI.Controls.Inventory;
using RR.UI.Pages;

namespace RaiderRoughPatches.Patch {
    internal static class RaiderRoughPatchesPatch {
        private static MethodInfo _transferItemMethod;

        public static void Apply(Harmony harmony, ConfigFile config) {
            var fixSessionVisibility = config.Bind(
                "Fixes", "SessionVisibilityFix", true,
                "Re-confirm Fusion region when lobby loads — fixes hosted session disappearing from server list after returning from a run.");


            var fixStashAutoStack = config.Bind(
                "Fixes", "StashAutoStack", true,
                "Auto-stack stackable items when transferring from champion inventory to stash.");

            if (fixSessionVisibility.Value) {
                SessionVisibilityFix.Init();

                var lobbySceneLoadDone = AccessTools.Method(typeof(LobbyManager), "OnSceneLoadDone");
                if (lobbySceneLoadDone == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: LobbyManager.OnSceneLoadDone not found — session visibility fix inactive.");
                } else {
                    harmony.Patch(lobbySceneLoadDone, postfix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(LobbyOnSceneLoadDonePostfix)));
                }
            }

            if (fixStashAutoStack.Value) {
                _transferItemMethod = AccessTools.Method(typeof(GameInventoryPage), "TransferItem");
                if (_transferItemMethod == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: GameInventoryPage.TransferItem not found — stash auto-stack inactive.");
                }

                if (StashStacker.OtherPanelField == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: GameInventoryPage._otherPanel not found — stash auto-stack inactive.");
                }

                if (_transferItemMethod != null && StashStacker.OtherPanelField != null) {
                    harmony.Patch(_transferItemMethod, prefix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(TransferItemPrefix)));
                }
            }

            RaiderRoughPatchesMod.PublicLogger.LogInfo("RaiderRoughPatches patches applied.");
        }

        private static void LobbyOnSceneLoadDonePostfix() =>
            SessionVisibilityFix.OnLobbySceneLoadDone();

        private static bool TransferItemPrefix(GameInventoryPage __instance, InventorySlot sourceSlot) =>
            StashStacker.TryAutoStack(__instance, sourceSlot);
    }
}
