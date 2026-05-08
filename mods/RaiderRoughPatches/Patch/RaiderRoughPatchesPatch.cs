using System.Reflection;
using BepInEx.Configuration;
using Fusion;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Perk;
using RR.Level;
using RR.UI.Controls.Inventory;
using RR.UI.Pages;

namespace RaiderRoughPatches.Patch {
    internal static class RaiderRoughPatchesPatch {
        // Method handles — resolved once in Init(), reused on every Patch() call.
        private static MethodInfo _lobbySceneLoadDone;
        private static MethodInfo _transferItemMethod;
        private static MethodInfo _canMergeItemMethod;
        private static MethodInfo _doAreaSelection;
        private static MethodInfo _doorActivate;
        private static MethodInfo _rpcVoteState;
        private static MethodInfo _onPlayerLeft;
        private static MethodInfo _sunStrikeFixedUpdate;
        private static MethodInfo _charBaseFixedUpdate;

        // Config entries — bound once in Init().
        private static ConfigEntry<bool> _fixSessionVisibility;
        private static ConfigEntry<bool> _fixStashAutoStack;
        private static ConfigEntry<bool> _fixBarrierSelfGrant;
        private static ConfigEntry<bool> _fixDoorVoteOnDisconnect;
        private static ConfigEntry<bool> _fixFusionStaleRefs;

        private static HarmonyMethod Fix(string methodName) =>
            new HarmonyMethod(typeof(RaiderRoughPatchesPatch), methodName) { priority = Priority.First };

        // Called once from Awake(). Binds config and resolves all reflection handles.
        internal static void Init(ConfigFile config) {
            _fixSessionVisibility = config.Bind(
                "Fixes", "SessionVisibilityFix", true,
                "Re-confirm Fusion region when lobby loads — fixes hosted session disappearing from server list after returning from a run.");

            _fixStashAutoStack = config.Bind(
                "Fixes", "StashAutoStack", true,
                "Auto-stack stackable items when transferring between champion inventory and stash.");

            _fixBarrierSelfGrant = config.Bind(
                "Fixes", "BarrierSelfGrantFix", true,
                "Re-apply 'self and nearest ally' perk effects to the caster — fixes effects only landing on allies in multiplayer due to _chooseOwnerLast removing the caster from the candidate pool.");

            _fixDoorVoteOnDisconnect = config.Bind(
                "Fixes", "DoorVoteOnDisconnectFix", true,
                "Re-evaluate door vote when a player disconnects mid-vote — fixes vote getting stuck when a non-slot-0 player leaves.");

            if (_fixSessionVisibility.Value) {
                SessionVisibilityFix.Init();

                _lobbySceneLoadDone = AccessTools.Method(typeof(LobbyManager), "OnSceneLoadDone");
                if (_lobbySceneLoadDone == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: LobbyManager.OnSceneLoadDone not found — session visibility fix inactive.");
                }
            }

            if (_fixStashAutoStack.Value) {
                ItemStackFix.Init();

                _transferItemMethod = AccessTools.Method(typeof(GameInventoryPage), "TransferItem");
                if (_transferItemMethod == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: GameInventoryPage.TransferItem not found — stash auto-stack inactive.");
                }

                _canMergeItemMethod = AccessTools.Method(typeof(InventorySlotNormal), nameof(InventorySlotNormal.CanMergeItem));
                if (_canMergeItemMethod == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: InventorySlotNormal.CanMergeItem not found — cross-item merge fix inactive.");
                }
            }

            if (_fixBarrierSelfGrant.Value) {
                BarrierSelfGrantFix.Init();

                if (BarrierSelfGrantFix.IsReady) {
                    _doAreaSelection = AccessTools.Method(typeof(AreaCharacterSelector), "DoAreaSelection");
                    if (_doAreaSelection == null) {
                        RaiderRoughPatchesMod.PublicLogger.LogWarning(
                            "RaiderRoughPatches: AreaCharacterSelector.DoAreaSelection not found — barrier self-grant fix inactive.");
                    }
                }
            }

            if (_fixDoorVoteOnDisconnect.Value) {
                DoorVoteFix.Init();

                _doorActivate = AccessTools.Method(typeof(DoorManager), "Activate");
                if (_doorActivate == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: DoorManager.Activate not found — door vote disconnect fix inactive.");
                }

                _rpcVoteState = AccessTools.Method(typeof(DoorManager), nameof(DoorManager.RPC_VoteState));
                if (_rpcVoteState == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: DoorManager.RPC_VoteState not found — door vote disconnect fix inactive.");
                }

                _onPlayerLeft = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerLeft));
                if (_onPlayerLeft == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: PlayerManager.OnPlayerLeft not found — door vote disconnect fix inactive.");
                }
            }

            _fixFusionStaleRefs = config.Bind(
                "Fixes", "FusionStaleRefFix", true,
                "Detect and clear fake-null Fusion NetworkBehaviourId backing fields (SunStrikeArea.Caster, NetworkCharacterBase.PullCenter) — fixes \"Failed to unwrap\" log spam at end of match.");

            if (_fixFusionStaleRefs.Value) {
                FusionStaleRefFix.Init();

                _sunStrikeFixedUpdate = AccessTools.Method(typeof(SunStrikeArea), "FixedUpdateNetwork");
                if (_sunStrikeFixedUpdate == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: SunStrikeArea.FixedUpdateNetwork not found — SunStrikeArea stale ref guard inactive.");
                }

                _charBaseFixedUpdate = AccessTools.Method(typeof(NetworkCharacterBase), "FixedUpdateNetwork");
                if (_charBaseFixedUpdate == null) {
                    RaiderRoughPatchesMod.PublicLogger.LogWarning(
                        "RaiderRoughPatches: NetworkCharacterBase.FixedUpdateNetwork not found — PullCenter stale ref guard inactive.");
                }
            }
        }

        // Re-callable from Enable(). Registers all patches for which handles were resolved.
        internal static void Patch(Harmony harmony) {
            if (_fixSessionVisibility?.Value == true && _lobbySceneLoadDone != null) {
                harmony.Patch(_lobbySceneLoadDone, postfix: Fix(nameof(LobbyOnSceneLoadDonePostfix)));
            }

            if (_fixStashAutoStack?.Value == true) {
                if (_transferItemMethod != null) {
                    harmony.Patch(_transferItemMethod, prefix: Fix(nameof(TransferItemPrefix)));
                }
                if (_canMergeItemMethod != null) {
                    harmony.Patch(_canMergeItemMethod, postfix: Fix(nameof(CanMergeItemPostfix)));
                }
            }

            if (_fixBarrierSelfGrant?.Value == true && _doAreaSelection != null) {
                harmony.Patch(_doAreaSelection, postfix: Fix(nameof(DoAreaSelectionPostfix)));
            }

            if (_fixDoorVoteOnDisconnect?.Value == true) {
                if (_doorActivate != null) {
                    harmony.Patch(_doorActivate, postfix: Fix(nameof(DoorActivatePostfix)));
                }

                if (_rpcVoteState != null) {
                    harmony.Patch(_rpcVoteState, postfix: Fix(nameof(RpcVoteStatePostfix)));
                }

                if (_onPlayerLeft != null) {
                    harmony.Patch(_onPlayerLeft,
                        prefix: Fix(nameof(PlayerLeftPrefix)),
                        postfix: Fix(nameof(PlayerLeftPostfix)));
                }
            }

            if (_fixFusionStaleRefs?.Value == true) {
                if (_sunStrikeFixedUpdate != null) {
                    harmony.Patch(_sunStrikeFixedUpdate, prefix: Fix(nameof(SunStrikeFixedUpdateNetworkPrefix)));
                }

                if (_charBaseFixedUpdate != null) {
                    harmony.Patch(_charBaseFixedUpdate, prefix: Fix(nameof(CharBaseFixedUpdateNetworkPrefix)));
                }
            }

            RaiderRoughPatchesMod.PublicLogger.LogInfo("RaiderRoughPatches patches applied.");
        }

        private static void LobbyOnSceneLoadDonePostfix() =>
            SessionVisibilityFix.OnLobbySceneLoadDone();

        private static bool TransferItemPrefix(GameInventoryPage __instance, InventorySlot sourceSlot) =>
            ItemStackFix.TryAutoStack(__instance, sourceSlot);

        private static void CanMergeItemPostfix(InventorySlotNormal __instance, InventoryItem sourceItem, ref bool __result) =>
            ItemStackFix.FixCanMergeItem(__instance, sourceItem, ref __result);

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

        private static bool SunStrikeFixedUpdateNetworkPrefix(SunStrikeArea __instance) =>
            FusionStaleRefFix.OnSunStrikeFixedUpdate(__instance);

        private static void CharBaseFixedUpdateNetworkPrefix(NetworkCharacterBase __instance) =>
            FusionStaleRefFix.OnCharBaseFixedUpdate(__instance);
    }
}
