using System.Collections.Generic;
using System.Reflection;
using Fusion;
using HarmonyLib;
using RR;
using RR.Level;

namespace RaiderRoughPatches {

    // Fixes DoorManager.CheckVotes() not being re-evaluated when a player
    // disconnects mid-vote. Two root causes:
    //
    //   1. CheckVotes() is only called from RPC_VoteState — never on disconnect.
    //   2. CheckVotes() checks hardcoded slot indices (0, 0+1, 0+1+2) so a
    //      remaining player in slot 2 after slot 1 disconnects is ignored.
    //
    // Fix: shadow-track active votes per slot. On disconnect, remove that
    // player's vote and check if all remaining connected players agree.
    // Uses OnVoteEnd() (private) via reflection — resolved once in Init().

    internal static class DoorVoteFix {
        // slotIndex → doorIndex for currently-connected players who have voted.
        private static readonly Dictionary<int, int> _slotVotes = new Dictionary<int, int>();

        // Captured in the PlayerLeft prefix (player still present) so the
        // postfix can act after the player object is removed.
        private static int _disconnectingSlot = -1;

        private static MethodInfo _onVoteEndMethod;

        internal static void Init() {
            _onVoteEndMethod = AccessTools.Method(typeof(DoorManager), "OnVoteEnd");
            if (_onVoteEndMethod == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: DoorManager.OnVoteEnd not found — door vote disconnect fix inactive.");
            }
        }

        // Called from the RPC_VoteState postfix (server only).
        // Keeps the shadow dict in sync with actual player votes.
        internal static void OnVoteCast(DoorManager door, int slotIdx, int selectedIdx) {
            if (door.Runner?.IsServer != true) { return; }
            if (selectedIdx < 0) {
                _slotVotes.Remove(slotIdx);
            } else {
                _slotVotes[slotIdx] = selectedIdx;
            }
        }

        // Called from the Activate postfix — door session is starting fresh.
        internal static void Reset() => _slotVotes.Clear();

        // Called from the PlayerLeft PREFIX — player still reachable in PlayerManager.
        internal static void CaptureDisconnectingSlot(PlayerRef playerRef) {
            _disconnectingSlot = -1;
            var mgr = PlayerManager.Instance;
            if (mgr == null) { return; }
            foreach (var player in mgr.GetPlayers()) {
                if (player?.Object?.InputAuthority == playerRef) {
                    _disconnectingSlot = player.SlotIndex;
                    return;
                }
            }
            RaiderRoughPatchesMod.PublicLogger.LogWarning(
                $"RaiderRoughPatches: could not find slot for PlayerRef {playerRef} — vote cleanup skipped.");
        }

        // Called from the PlayerLeft POSTFIX — player is gone, PlayerCount decremented.
        internal static void OnPlayerLeft() {
            if (_disconnectingSlot < 0) { return; }

            _slotVotes.Remove(_disconnectingSlot);
            _disconnectingSlot = -1;

            if (_onVoteEndMethod == null) { return; }

            var door = DoorManager.Instance;
            if (door == null) { return; }
            if (door.Runner?.IsServer != true) { return; }
            if (door.State != DoorManager.DoorState.Activated) { return; }

            var mgr = PlayerManager.Instance;
            if (mgr == null) { return; }

            int playerCount = mgr.PlayerCount;
            if (_slotVotes.Count != playerCount) { return; }

            int? agreedDoor = null;
            foreach (var vote in _slotVotes.Values) {
                if (agreedDoor == null) {
                    agreedDoor = vote;
                } else if (agreedDoor.Value != vote) {
                    return;
                }
            }

            if (agreedDoor.HasValue) {
                RaiderRoughPatchesMod.PublicLogger.LogInfo(
                    $"RaiderRoughPatches: player disconnected mid-vote; resolving with door {agreedDoor.Value}.");
                _onVoteEndMethod.Invoke(door, new object[] { agreedDoor.Value });
            }
        }
    }
}
