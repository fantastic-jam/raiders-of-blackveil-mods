using System;
using System.Collections.Generic;
using Fusion;
using RR;
using RR.Backend.API.V1.Ingress.Message;
using UnityEngine;

namespace SpectateMode {
    /// <summary>
    /// Owns the server-authoritative pre-join list. A pre-joiner is a Fusion peer
    /// connected to the host but with no <see cref="Player"/> network object spawned —
    /// they are invisible to every game system that iterates <c>PlayerManager._activePlayers</c>.
    /// They are promoted to a real Player at the start of the next room load
    /// (<c>GameManager.NextLevel</c>), going through the same vanilla spawn chain as
    /// a lobby joiner.
    /// </summary>
    internal static class SpectateModeManager {
        private static readonly HashSet<PlayerRef> _preJoiners = new HashSet<PlayerRef>();

        internal static int PreJoinerCount => _preJoiners.Count;

        internal static bool IsPreJoiner(PlayerRef playerRef) => _preJoiners.Contains(playerRef);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        internal static void Reset() => _preJoiners.Clear();

        // ── Join / leave hooks ────────────────────────────────────────────────

        /// <summary>
        /// Called from <c>PlayerManager.OnPlayerJoined</c> prefix on the server.
        /// Returns true if the join should be intercepted (run is in progress) — the
        /// caller must skip the vanilla spawn. Returns false to let vanilla run.
        /// </summary>
        internal static bool TryBeginPreJoin(NetworkRunner runner, PlayerRef playerRef) {
            if (runner == null || !runner.IsServer) { return false; }
            if (GameManager.Instance == null || !GameManager.Instance.IsInActiveRun) { return false; }

            if (_preJoiners.Add(playerRef)) {
                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: pre-joining ref={playerRef.PlayerId} (active={PlayerManager.Instance?.GetPlayers().Count ?? 0}, pre={_preJoiners.Count}).");
                WmfChatBridge.HostNotify("A new player is joining. They will spawn in at the start of the next room.");
                SendBackendCountUpdate(IngressMessagePlaySessionUpdateEvent.EventType.PlayerJoinedSession);
            }
            return true;
        }

        /// <summary>
        /// Called from <c>PlayerManager.OnPlayerLeft</c> prefix on the server.
        /// Returns true if the leaver was a pre-joiner — caller must skip the vanilla
        /// despawn (no Player object exists). Returns false to let vanilla run.
        /// </summary>
        internal static bool TryCancelPreJoin(NetworkRunner runner, PlayerRef playerRef) {
            if (runner == null || !runner.IsServer) { return false; }
            if (!_preJoiners.Remove(playerRef)) { return false; }

            SpectateModeMod.PublicLogger.LogInfo(
                $"SpectateMode: pre-joiner ref={playerRef.PlayerId} disconnected — dropped (pre={_preJoiners.Count}).");
            SendBackendCountUpdate(IngressMessagePlaySessionUpdateEvent.EventType.PlayerLeftSession);
            return true;
        }

        // Manually advertises the bumped player count to the backend so the lobby
        // browser reflects pre-joiners. Mirrors the vanilla {player_id, player_count}
        // shape; we don't have a profile UUID for a pre-joiner, so we send Guid.Empty
        // — the backend treats this as a count-bump notification. When the pre-joiner
        // is later promoted at LevelWin, the vanilla RPC_PlayerJoinedPlaySession path
        // fires its own PlayerJoinedSession with the real UUID, reconciling state.
        private static void SendBackendCountUpdate(IngressMessagePlaySessionUpdateEvent.EventType eventType) {
            try {
                var pm = PlayerManager.Instance;
                var metrics = MetricsManager.Instance;
                if (pm == null || metrics == null) { return; }
                int count = pm.GetPlayers().Count + _preJoiners.Count;
                metrics.SendPlaySessionUpdateEvent(eventType, new {
                    player_id = Guid.Empty.ToString(),
                    player_count = count,
                });
            }
            catch (Exception ex) {
                SpectateModeMod.PublicLogger.LogWarning(
                    $"SpectateMode: failed to send {eventType} backend update: {ex.Message}");
            }
        }

        // ── Promotion ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called from <c>GameManager.NextLevel</c> prefix on the server. By this
        /// point the previous room's exit cutscene has finished, every existing
        /// client has reported in via <c>DungeonManager.RPC_ObjectsCleared</c> (the
        /// gate at <c>_closedLevelCount &gt;= GetPlayers().Count</c> has already
        /// passed), and the backend per-player state save has completed. Spawning
        /// the pre-joiners' Player objects here means they are added to
        /// <c>_activePlayers</c> *after* the gate cleared, and they exist in the
        /// simulation by the time <c>NextLevel</c> calls
        /// <c>LevelLoadingHandler.RPC_StartSceneLoad</c> in its body.
        /// <para>
        /// The vanilla chain (<c>Player.Spawned → AddPlayer</c>,
        /// <c>AfterSpawned → RPC_PlayerJoinedPlaySession → champion spawn</c>,
        /// scene transition with DDOL, <c>IntroManager.RPC_IntroActivation →
        /// InitPlayerCharacterAtSpawnPoint</c>,
        /// <c>Handle_LevelEvent_IntroFinished</c> → input enabled) takes over from
        /// here exactly as it does for a lobby joiner.
        /// </para>
        /// </summary>
        internal static void PromoteAll() {
            if (_preJoiners.Count == 0) { return; }

            var pm = PlayerManager.Instance;
            var runner = pm?.Runner;
            if (pm == null || runner == null || !runner.IsServer || pm.PlayerPrefab == null) {
                SpectateModeMod.PublicLogger.LogWarning(
                    "SpectateMode: PromoteAll skipped — PlayerManager / Runner / PlayerPrefab not ready.");
                return;
            }

            // Snapshot first — runner.Spawn → Player.Spawned → AddPlayer can run
            // synchronously and we don't want to mutate _preJoiners mid-iteration.
            var snapshot = new List<PlayerRef>(_preJoiners);
            _preJoiners.Clear();

            foreach (var playerRef in snapshot) {
                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: promoting pre-joiner ref={playerRef.PlayerId} — spawning Player prefab.");
                runner.Spawn(pm.PlayerPrefab, Vector3.zero, Quaternion.identity, playerRef);
            }
        }

        // ── Block "run started" backend signal ────────────────────────────────

        /// <summary>
        /// Called from <c>MetricsManager.SendPlaySessionUpdateEvent</c> prefix.
        /// Returns false (skip) for <see cref="IngressMessagePlaySessionUpdateEvent.EventType.LobbyEnd"/>
        /// so the backend never marks the session as "no longer in lobby" — keeps it joinable.
        /// </summary>
        internal static bool ShouldSendUpdateEvent(IngressMessagePlaySessionUpdateEvent.EventType eventType) =>
            eventType != IngressMessagePlaySessionUpdateEvent.EventType.LobbyEnd;
    }
}
