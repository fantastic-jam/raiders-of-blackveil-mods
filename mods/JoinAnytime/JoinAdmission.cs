using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using RR;
using RR.Backend.Phoenix;
using UnityEngine;

namespace JoinAnytime {
    /// <summary>
    /// Lets anyone join a run in progress by making them look like a Phoenix-recoverable party member.
    /// <para>
    /// The game already re-admits and fully restores returning raid members mid-run: it accepts a connection
    /// whose token GUID is in <c>PhoenixManager.CurrentRunReconnect.SavedParty</c>, spawns the champion next to
    /// the host, applies the saved member state, and runs the client late-join init (HUD, doors, shrine and
    /// PerkPlus catch-up) — no mod needed on the joining client. On connect we insert a fabricated member for the
    /// newcomer (see <see cref="NewcomerFactory"/>) and let vanilla do everything else.
    /// </para>
    /// </summary>
    internal static class JoinAdmission {
        // Drop a fabricated member if its newcomer never shows up as a Fusion player within this delay.
        private const float ConnectTimeoutSeconds = 60f;

        private sealed class Pending {
            internal PhoenixPartyMember Member;
            internal float AdmittedAt;
            internal PlayerRef? PlayerRef; // set once the newcomer's Fusion player joined
        }

        // Fabricated members not yet consumed by the game's restore. Host-side.
        private static readonly Dictionary<Guid, Pending> _pending = new();

        internal static void Reset() => _pending.Clear();

        // ── Hooks ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Host, before vanilla <c>NetworkManager.OnConnectRequest</c>: if the raid is in progress and the joiner
        /// isn't already a raid member, insert a fabricated Phoenix member so vanilla accepts them.
        /// </summary>
        internal static void OnConnectRequest(NetworkRunner runner, byte[] token) {
            if (!runner.IsServer) { return; }
            var gm = GameManager.Instance;
            var pm = PlayerManager.Instance;
            if (gm == null || pm == null || gm.IsLobbyNoRaidStartedYet()) { return; }   // lobby: vanilla accepts
            if (pm.GetPlayers().Count > 2) { return; }                                  // full: vanilla refuses
            if (!TryGetTokenPlayerId(token, out var playerId)) { return; }               // no identity: vanilla refuses
            if (pm.IsRaidMember(playerId) || PhoenixManager.IsRecoverablePartyMember(playerId)) { return; } // returning: vanilla

            var checkpoint = PhoenixManager.CurrentRunReconnect;
            if (checkpoint?.SavedParty == null) {
                // The first checkpoint is saved once level 1 has loaded — nothing to restore into before that.
                JoinAnytimeMod.PublicLogger.LogInfo($"JoinAnytime: {playerId} wants to join, but the run has no checkpoint yet — refused for now.");
                return;
            }

            var member = NewcomerFactory.BuildSkeleton(playerId);
            if (member == null) {
                JoinAnytimeMod.PublicLogger.LogWarning($"JoinAnytime: could not build a party member for {playerId} — refused.");
                return;
            }

            checkpoint.SavedParty.Add(member);
            // After a host crash-recovery CurrentRunReconnect is also PendingRecovery, which re-applies unapplied
            // members at every level start — mark ours applied so it's never re-applied on top of live state.
            PhoenixManager.MarkMemberApplied(playerId);
            _pending[playerId] = new Pending { Member = member, AdmittedAt = Time.realtimeSinceStartup };

            JoinAnytimeMod.PublicLogger.LogInfo(
                $"JoinAnytime: admitting {playerId} mid-run (xp={member.ChampionXPAmount}, abilityPoints={member.ChampionAbilityPoints}).");
            WmfChatBridge.HostNotify("A new player is joining the run.");
        }

        /// <summary>Host, after <c>PlayerManager.OnPlayerJoined</c>: remember which Fusion player a pending newcomer is.</summary>
        internal static void OnPlayerJoined(NetworkRunner runner, PlayerRef playerRef) {
            if (!runner.IsServer || _pending.Count == 0) { return; }
            if (!TryGetTokenPlayerId(runner.GetPlayerConnectionToken(playerRef), out var playerId)) { return; }
            if (_pending.TryGetValue(playerId, out var pending)) { pending.PlayerRef = playerRef; }
        }

        /// <summary>Host, before <c>PlayerManager.OnPlayerLeft</c>: a newcomer leaving before being restored is dropped.</summary>
        internal static void OnPlayerLeft(PlayerRef playerRef) {
            var playerId = _pending.FirstOrDefault(kv => kv.Value.PlayerRef == playerRef).Key;
            if (playerId == Guid.Empty) { return; }
            Discard(playerId, "left before spawning");
        }

        /// <summary>
        /// Host, after <c>PhoenixManager.TryConsumeReconnectRestore</c>: the game is about to apply the member to the
        /// newcomer's freshly spawned champion — roll their perks now that the champion and slot exist.
        /// </summary>
        internal static void OnRestoreConsumed(bool consumed, Guid playerId, PhoenixPartyMember member) {
            if (!consumed || member == null || !_pending.Remove(playerId)) { return; }
            var player = PlayerManager.Instance?.GetPlayers().FirstOrDefault(p => p.ProfileUUID == playerId);
            if (player == null) { return; }
            try {
                NewcomerFactory.FillPerks(member, player);
                JoinAnytimeMod.PublicLogger.LogInfo(
                    $"JoinAnytime: restoring newcomer {playerId} — perks lvl0={member.CollectedPerkIdsLevel0.Count}, " +
                    $"lvl1={member.CollectedPerkIdsLevel1.Count}, lvl2={member.CollectedPerkIdsLevel2.Count}.");
            }
            catch (Exception ex) {
                JoinAnytimeMod.PublicLogger.LogWarning($"JoinAnytime: rolling perks for {playerId} failed — joining without perks. {ex.Message}");
            }
        }

        /// <summary>Host, every network tick: drop fabricated members whose newcomer never connected.</summary>
        internal static void PruneStale() {
            if (_pending.Count == 0) { return; }
            var now = Time.realtimeSinceStartup;
            foreach (var playerId in _pending
                         .Where(kv => kv.Value.PlayerRef == null && now - kv.Value.AdmittedAt > ConnectTimeoutSeconds)
                         .Select(kv => kv.Key)
                         .ToList()) {
                Discard(playerId, "never connected");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // A fabricated entry left in the checkpoint would keep the GUID "recoverable" for the whole run and put a
        // ghost in the roster restored after a host crash. SaveCheckpoint rebuilds the list each level (carrying
        // non-live members over), so remove by id rather than by reference.
        private static void Discard(Guid playerId, string reason) {
            _pending.Remove(playerId);
            var removed = PhoenixManager.CurrentRunReconnect?.SavedParty?.RemoveAll(m => m.PlayerId == playerId) ?? 0;
            JoinAnytimeMod.PublicLogger.LogInfo($"JoinAnytime: dropped newcomer {playerId} ({reason}, {removed} checkpoint entr{(removed == 1 ? "y" : "ies")} removed).");
        }

        // Mirrors NetworkManager.TryGetConnectionTokenPlayerId: the game sends the profile GUID, exactly 16 bytes.
        private static bool TryGetTokenPlayerId(byte[] token, out Guid playerId) {
            playerId = Guid.Empty;
            if (token == null || token.Length != 16) { return false; }
            playerId = new Guid(token);
            return playerId != Guid.Empty;
        }
    }
}
