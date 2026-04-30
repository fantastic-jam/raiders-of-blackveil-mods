using System.Reflection;
using BepInEx.Configuration;
using Fusion;
using HarmonyLib;
using RR;
using RR.Game.Perk;
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

            var fixDoorVoteOnDisconnect = config.Bind(
                "Fixes", "DoorVoteOnDisconnectFix", true,
                "Re-evaluate door vote when a player disconnects mid-vote — fixes vote getting stuck when a non-slot-0 player leaves.");

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

            var fixBarrierSelfGrant = config.Bind(
                "Fixes", "BarrierSelfGrantFix", true,
                "Re-apply 'self and nearest ally' perk effects to the caster — fixes effects only landing on allies in multiplayer due to _chooseOwnerLast removing the caster from the candidate pool.");

            if (fixBarrierSelfGrant.Value) {
                BarrierSelfGrantFix.Init();

                var doAreaSelection = AccessTools.Method(typeof(AreaCharacterSelector), "DoAreaSelection");
                if (doAreaSelection == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: AreaCharacterSelector.DoAreaSelection not found — barrier self-grant fix inactive.");
                } else if (BarrierSelfGrantFix.IsReady) {
                    harmony.Patch(doAreaSelection, postfix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(DoAreaSelectionPostfix)));
                }
            }

            if (fixDoorVoteOnDisconnect.Value) {
                DoorVoteFix.Init();

                var doorActivate = AccessTools.Method(typeof(DoorManager), "Activate");
                if (doorActivate == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: DoorManager.Activate not found — door vote disconnect fix inactive.");
                } else {
                    harmony.Patch(doorActivate, postfix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(DoorActivatePostfix)));
                }

                var rpcVoteState = AccessTools.Method(typeof(DoorManager), nameof(DoorManager.RPC_VoteState));
                if (rpcVoteState == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: DoorManager.RPC_VoteState not found — door vote disconnect fix inactive.");
                } else {
                    harmony.Patch(rpcVoteState, postfix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(RpcVoteStatePostfix)));
                }

                var onPlayerLeft = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerLeft));
                if (onPlayerLeft == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: PlayerManager.OnPlayerLeft not found — door vote disconnect fix inactive.");
                } else {
                    harmony.Patch(onPlayerLeft,
                        prefix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(PlayerLeftPrefix)),
                        postfix: new HarmonyMethod(typeof(RaiderRoughPatchesPatch), nameof(PlayerLeftPostfix)));
                }
            }

            RaiderRoughPatchesMod.PublicLogger.LogInfo("RaiderRoughPatches patches applied.");
        }

        private static void LobbyOnSceneLoadDonePostfix() =>
            SessionVisibilityFix.OnLobbySceneLoadDone();

        private static bool TransferItemPrefix(GameInventoryPage __instance, InventorySlot sourceSlot) =>
            StashStacker.TryAutoStack(__instance, sourceSlot);

        private static void DoorActivatePostfix() =>
            DoorVoteFix.Reset();

        private static void RpcVoteStatePostfix(DoorManager __instance, int slotIdx, int selectedIdx) =>
            DoorVoteFix.OnVoteCast(__instance, slotIdx, selectedIdx);

        private static void PlayerLeftPrefix(PlayerRef playerRef) =>
            DoorVoteFix.CaptureDisconnectingSlot(playerRef);

        private static void PlayerLeftPostfix() =>
            DoorVoteFix.OnPlayerLeft();

        private static void DoAreaSelectionPostfix(AreaCharacterSelector __instance) =>
            BarrierSelfGrantFix.OnDoAreaSelectionDone(__instance);
    }
}

