using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using RR;
using RR.Backend.API.V1.Ingress.Message;
using RR.Game;
using RR.Game.Character;
using RR.Game.Pickups;
using RR.Game.Perk;
using RR.Level;
using UnityEngine;
using WildguardModFramework.Network;

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
        private static readonly HashSet<PlayerRef> _preJoiners = new();

        // Tracks promoted PlayerRefs waiting for their champion to spawn so averaging can be applied.
        private static readonly HashSet<PlayerRef> _pendingAveraging = new();

        // Tracks promoted PlayerRefs waiting to be placed at their spawn point by IntroManager.
        // Cleared in TryPlaceAtSpawnPoint after IntroManager.RPC_IntroActivation fires.
        private static readonly HashSet<PlayerRef> _pendingPlacement = new();

        // Pre-joiners confirmed to have WMF/SpectateMode installed.
        private static readonly HashSet<PlayerRef> _moddedPreJoiners = new();

        // Tracks PlayerRefs promoted mid-run without the mod — their ShrineHandler._perPlayerData
        // was never initialized so they get perk choice pickups instead of a shrine.
        private static readonly HashSet<PlayerRef> _unmoddedPromotedJoiners = new();

        // Tracks the 3 perk choice pickups spawned per unmodded joiner so siblings can be
        // despawned when one is collected.
        private static readonly Dictionary<PlayerRef, List<NetworkObject>> _unmoddedPerkChoices = new();

        internal static int PreJoinerCount => _preJoiners.Count;

        // RpcTriggerLevelExitPostfix checks this to decide whether to call ShrineHandler.RunStart().
        // It runs before NextLevel (which calls PromoteAll, which clears _moddedPreJoiners), so
        // the flag is still set when the postfix fires.
        internal static bool HasModdedPreJoiner => _moddedPreJoiners.Count > 0;

        internal static bool IsPreJoiner(PlayerRef playerRef) => _preJoiners.Contains(playerRef);

        internal static bool IsUnmoddedPromotedJoiner(PlayerRef playerRef) => _unmoddedPromotedJoiners.Contains(playerRef);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        internal static void Reset() {
            _preJoiners.Clear();
            _pendingAveraging.Clear();
            _pendingPlacement.Clear();
            _moddedPreJoiners.Clear();
            _unmoddedPromotedJoiners.Clear();
            _unmoddedPerkChoices.Clear();
        }

        // ── Mod detection ─────────────────────────────────────────────────────

        /// <summary>
        /// Called on the host via <c>WmfNetwork.Subscribe("spectatemode:present", …)</c>.
        /// The joining client sends this message when they detect they are mid-run spectating,
        /// which requires SpectateMode to be installed on their end. "isModded" from WMF's
        /// generic <c>OnPlayerConfirmed</c> is NOT used — everyone has WMF as a dependency.
        /// </summary>
        internal static void OnSpectateModePresentReceived(PlayerRef playerRef, byte[] _) {
            if (!_preJoiners.Contains(playerRef)) { return; }
            if (_moddedPreJoiners.Add(playerRef)) {
                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: pre-joiner ref={playerRef.PlayerId} confirmed SpectateMode installed.");
            }
        }

        // ── Join / leave hooks ────────────────────────────────────────────────

        /// <summary>
        /// Called from <c>PlayerManager.OnPlayerJoined</c> prefix on all machines.
        /// On the server: delegates to <see cref="TryBeginPreJoin"/> and returns its result.
        /// On clients: if this is the local player joining mid-run, sends the
        /// SpectateMode presence message to the host so the host can detect mod presence.
        /// Always returns false on clients (vanilla spawn must proceed normally).
        /// </summary>
        internal static bool TryHandleOnPlayerJoined(NetworkRunner runner, PlayerRef playerRef) {
            if (runner == null) { return false; }
            if (runner.IsServer) { return TryBeginPreJoin(runner, playerRef); }
            if (playerRef == runner.LocalPlayer && GameManager.Instance?.IsInActiveRun == true) {
                WmfNetwork.SendToHost("spectatemode:present", System.Array.Empty<byte>());
            }
            return false;
        }

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

            // Also clean up any promotion-phase state in case they disconnect after PromoteAll
            // but before their champion spawns or a shrine activates.
            _pendingAveraging.Remove(playerRef);
            _pendingPlacement.Remove(playerRef);
            _unmoddedPromotedJoiners.Remove(playerRef);

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
                bool isModded = _moddedPreJoiners.Contains(playerRef);
                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: promoting pre-joiner ref={playerRef.PlayerId} (modded={isModded}) — spawning Player prefab.");
                _pendingAveraging.Add(playerRef);
                _pendingPlacement.Add(playerRef);
                if (!isModded) {
                    _unmoddedPromotedJoiners.Add(playerRef);
                }
                runner.Spawn(pm.PlayerPrefab, Vector3.zero, Quaternion.identity, playerRef);
            }
            _moddedPreJoiners.Clear();
        }

        /// <summary>
        /// Called server-side instead of spawning a shrine for a promoted unmodded joiner.
        /// Their client never ran <c>ShrineHandler.SceneInit()</c> so <c>_perPlayerData</c>
        /// is uninitialised — a shrine would be broken for them. Instead we spawn three
        /// <see cref="PerkPickup"/> NetworkObjects assigned to that player: they walk over one
        /// to collect it, and the other two are despawned via
        /// <see cref="OnPerkPickupCollected"/>.
        /// </summary>
        internal static void SpawnPerkChoices(NetworkRunner runner, PlayerRef playerRef, int slotIndex) {
            var player = PlayerManager.Instance?.GetPlayerBySlot(slotIndex);
            if (player?.PlayableChampion == null) {
                SpectateModeMod.PublicLogger.LogWarning(
                    $"SpectateMode: SpawnPerkChoices — no champion for slot {slotIndex}, skipping.");
                return;
            }

            var prefab = RewardManager.Instance?.RewardPerkPickupPrefab;
            if (prefab == null) {
                SpectateModeMod.PublicLogger.LogWarning(
                    "SpectateMode: SpawnPerkChoices — RewardPerkPickupPrefab not available.");
                return;
            }

            var perks = PerkDatabase.Instance.GetRandomPerkAmount(
                3, Category.None, Rarity.Common, player.PlayerFilter, new List<PerkDescriptor>());
            if (perks.Count == 0) { return; }

            // Despawn any leftover choices from a previous shrine room (shouldn't normally happen).
            if (_unmoddedPerkChoices.TryGetValue(playerRef, out var stale)) {
                foreach (var obj in stale) {
                    if (obj != null && obj.IsValid) { runner.Despawn(obj); }
                }
            }

            var basePos = player.PlayableChampion.transform.position;
            var pickups = new List<NetworkObject>(perks.Count);

            for (int i = 0; i < perks.Count; i++) {
                var perk = perks[i];
                float angle = i * (360f / perks.Count) * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Sin(angle) * 1.5f, 0f, Mathf.Cos(angle) * 1.5f);
                var spawned = runner.Spawn(prefab, basePos + offset, Quaternion.identity, playerRef,
                    (_, no) => {
                        var pickup = no.GetComponent<PerkPickup>();
                        pickup.PerkID = perk.PerkID;
                        pickup.Category = perk.Category;
                    });
                if (spawned != null) { pickups.Add(spawned); }
            }

            if (pickups.Count > 0) {
                _unmoddedPerkChoices[playerRef] = pickups;
                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: spawned {pickups.Count} perk choices for unmodded joiner ref={playerRef.PlayerId}.");
            }
        }

        /// <summary>
        /// Called from the <c>RewardManager.RPC_OnPerkPickup</c> postfix on the server.
        /// When a pickup belonging to an unmodded joiner's choice set is collected,
        /// despawns the remaining unchosen pickups.
        /// </summary>
        internal static void OnPerkPickupCollected(NetworkRunner runner, NetworkObject pickup) {
            if (!runner.IsServer) { return; }
            foreach (var kvp in _unmoddedPerkChoices) {
                if (!kvp.Value.Contains(pickup)) { continue; }
                foreach (var sibling in kvp.Value) {
                    if (sibling != pickup && sibling != null && sibling.IsValid) {
                        runner.Despawn(sibling);
                    }
                }
                _unmoddedPerkChoices.Remove(kvp.Key);
                return;
            }
        }

        // ── Spawn point placement ─────────────────────────────────────────────

        /// <summary>
        /// Called from the <c>IntroManager.RPC_IntroActivation</c> postfix on the server.
        /// IntroManager's own loop already calls <c>InitPlayerCharacterAtSpawnPoint</c> for
        /// every player whose champion is ready. This postfix covers the race where a promoted
        /// joiner's champion finishes spawning after that loop ran, leaving them stuck at
        /// <c>Vector3.zero</c>. For joiners already placed by IntroManager the call is a
        /// harmless double-teleport to the same point.
        /// </summary>
        internal static void TryPlaceAtSpawnPoint() {
            if (_pendingPlacement.Count == 0) { return; }
            var runner = PlayerManager.Instance?.Runner;
            if (runner == null || !runner.IsServer) { return; }
            var lm = GameManager.Instance?.GetLevelManager();
            if (lm == null) { return; }

            var snapshot = new List<PlayerRef>(_pendingPlacement);
            _pendingPlacement.Clear();

            foreach (var playerRef in snapshot) {
                var player = PlayerManager.Instance?.GetPlayerByRef(playerRef);
                if (player?.PlayableChampion == null) { continue; }
                lm.InitPlayerCharacterAtSpawnPoint(player);
                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: forced spawn-point placement for promoted joiner ref={playerRef.PlayerId}.");
            }
        }

        // ── Join averaging ────────────────────────────────────────────────────

        /// <summary>
        /// Called from the <c>NetworkChampionBase.AfterSpawned</c> postfix on the server.
        /// Applies floor-averaged XP, ability levels, and random perks to bring the newly
        /// promoted joiner up to the same ballpark as the existing players.
        /// </summary>
        internal static void TryApplyAveraging(NetworkChampionBase champion) {
            if (!champion.HasStateAuthority) { return; }
            var player = champion.Player;
            if (player == null) { return; }
            if (!_pendingAveraging.Remove(player.FusionPlayerRef)) { return; }

            try {
                var existing = PlayerManager.Instance.GetPlayers()
                    .Where(p => p != player && p.PlayableChampion != null)
                    .ToList();
                if (existing.Count == 0) { return; }

                champion.XP.Amount = (int)existing.Average(p => (double)p.PlayableChampion.XP.Amount);

                int avgPerkCount = (int)existing.Average(p => (double)p.PlayableChampion.PerkHandler.CollectedPerks.Count);
                var perksBySlot = existing
                    .Select(p => p.PlayableChampion.PerkHandler.CollectedPerks
                        .OrderBy(pk => (int)pk.Rarity).ToList())
                    .ToList();
                GiveRandomPerks(champion, player, avgPerkCount, perksBySlot);

                SpectateModeMod.PublicLogger.LogInfo(
                    $"SpectateMode: averaging applied to ref={player.FusionPlayerRef.PlayerId} — xp={champion.XP.Amount}, perks={avgPerkCount}.");
            }
            catch (Exception ex) {
                SpectateModeMod.PublicLogger.LogWarning(
                    $"SpectateMode: TryApplyAveraging failed for ref={player.FusionPlayerRef.PlayerId}: {ex.Message}");
            }
        }

        private static void GiveRandomPerks(NetworkChampionBase champion, Player player, int count, List<List<PerkDescriptor>> perksBySlot) {
            var perkHandler = champion.PerkHandler;
            if (perkHandler == null || count <= 0) { return; }
            var ignore = new List<PerkDescriptor>(perkHandler.CollectedPerks);
            for (int i = 0; i < count; i++) {
                var rarity = perksBySlot != null ? AverageRarityAtSlot(perksBySlot, i) : Rarity.Common;
                var result = PerkDatabase.Instance.GetRandomPerkAmount(1, Category.None, rarity, player.PlayerFilter, ignore);
                if (result.Count == 0) { break; }
                perkHandler.CollectPerkOnHost(result[0]);
                ignore.Add(result[0]);
            }
        }

        private static Rarity AverageRarityAtSlot(List<List<PerkDescriptor>> perksBySlot, int slot) {
            var values = perksBySlot
                .Where(list => slot < list.Count)
                .Select(list => (int)list[slot].Rarity)
                .ToList();
            return values.Count == 0 ? Rarity.Common : (Rarity)(int)values.Average();
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
