using System.Collections;
using RR;
using RR.Game;
using RR.Game.Character;
using RR.Level;
using ThePit.FeralEngine;
using ThePit.Patch;
using ThePit.FeralEngine.Abilities;
using UnityEngine;

namespace ThePit {
    internal class MatchController : MonoBehaviour {
        private static float MatchDurationSeconds =>
            ThePitState.MatchDurationSecondsOverride > 0f
                ? ThePitState.MatchDurationSecondsOverride
                : (ThePitMod.CfgMatchDurationSeconds?.Value ?? 600f);
        private const float ArenaGracePeriodSeconds = 10f;
        private const float RespawnDelaySeconds = 3f;
        private const float RespawnInvincibilitySeconds = 10f;
        private const float EndSequenceDelaySeconds = 20f;

        private static MatchController _instance;

        // Called from first room: create the object so TriggerRespawn has a target.
        internal static void CreateInstance() {
            var go = new GameObject("ThePit_Match");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MatchController>();
        }

        // Called when EventBeginLevel fires for the SlashBash room.
        internal static void StartArena() {
            if (_instance == null) { return; }
            FeralCore.Activate();
            ThePitState.CachedDamageReductionFactor = ThePitState.ResolvedDamageReductionFactor;
            // Champions spawned in the lobby before ArenaEntered was true — expand now.
            AbilityPatch.ExpandAllCasters();
            _instance.StartCoroutine(_instance.ArenaGraceCoroutine());
            _instance.StartCoroutine(_instance.MatchTimerCoroutine());
        }

        internal static void TriggerRespawn(int victimActorId) {
            if (_instance == null) { return; }
            _instance.StartCoroutine(_instance.RespawnCoroutine(victimActorId));
        }

        // Called on manual lobby return — stops the timer and all coroutines.
        internal static void Stop() {
            if (_instance == null) { return; }
            FeralCore.Deactivate();
            _instance.StopAllCoroutines();
            Destroy(_instance.gameObject);
            _instance = null;
        }

        // ── Arena grace period ───────────────────────────────────────────────────

        // AllDamageDisabled is already true from DungeonManager.OnSceneLoadDone.
        // Record deadlines and schedule clear — no need to set AllDamageDisabled=true.
        private IEnumerator ArenaGraceCoroutine() {
            // Wait one frame so InitPlayerCharacterAtSpawnPoint has placed champions.
            yield return null;
            float deadline = Time.time + ArenaGracePeriodSeconds;
            var doorGo = GameObject.Find("DoorSpawnPoint");
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                var champ = p.PlayableChampion;
                if (champ == null) { continue; }
                if (doorGo != null) { champ.LookToPosition(doorGo.transform.position); }
                FeralCore.GrantRespawnInvincibility(champ.Stats.ActorID, ArenaGracePeriodSeconds);
                StartCoroutine(ClearInvincibilityCoroutine(champ.Stats.ActorID, deadline));
            }
            yield return new WaitForSeconds(ArenaGracePeriodSeconds);
            var dm = DifficultyManager.Instance;
            if (dm != null) {
                float startPrecise = MatchDurationSeconds - ArenaGracePeriodSeconds;
                ThePitPatch.CombatTimePreciseSetter?.Invoke(dm, new object[] { startPrecise });
                ThePitPatch.CombatTimeInSecSetter?.Invoke(dm, new object[] { (int)Mathf.Ceil(startPrecise) });
            }
            ThePitState.CombatStarted = true;
        }

        // Clears AllDamageDisabled only if our deadline is still the current one.
        // If a new respawn fired in between, InvincibleUntil was updated and we bail.
        private IEnumerator ClearInvincibilityCoroutine(int actorId, float deadline) {
            yield return new WaitUntil(() => Time.time >= deadline);
            if (!FeralCore.TryGetInvincibilityDeadline(actorId, out float current) || current != deadline) {
                yield break;
            }
            FeralCore.RemoveInvincibility(actorId);
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                if (p.PlayableChampion?.Stats?.ActorID == actorId) {
                    p.PlayableChampion.Stats.Health.AllDamageDisabled = false;
                    break;
                }
            }
        }

        // ── Match timer ──────────────────────────────────────────────────────────

        private IEnumerator MatchTimerCoroutine() {
            yield return new WaitForSeconds(MatchDurationSeconds);
            EndMatch();
        }

        private void EndMatch() {
            var dm = DifficultyManager.Instance;
            if (dm != null) {
                ThePitPatch.CombatTimePreciseSetter?.Invoke(dm, new object[] { 0f });
                ThePitPatch.CombatTimeInSecSetter?.Invoke(dm, new object[] { 0 });
            }

            ThePitState.MatchEnded = true;

            int winnerActorId = DetermineWinner();

            // Kill all players who aren't the winner.
            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ == null) { continue; }
                if (champ.Stats.ActorID != winnerActorId && champ.Stats.Health.IsAlive) {
                    champ.Stats.Health.Die();
                }
            }

            StartCoroutine(ReturnToLobbyCoroutine());
        }

        private static int DetermineWinner() {
            int winnerActorId = -1;
            int maxKills = -1;
            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ == null) { continue; }
                int id = champ.Stats.ActorID;
                ThePitState.KillCounts.TryGetValue(id, out int kills);
                if (kills > maxKills) {
                    maxKills = kills;
                    winnerActorId = id;
                }
            }
            return winnerActorId;
        }

        // Called by the door patch when the winner steps on the trap door before the timer fires.
        internal static void TriggerEarlyLobbyReturn() {
            if (_instance == null) { return; }
            _instance.StopAllCoroutines();
            _instance.DoReturnToLobby();
        }

        private IEnumerator ReturnToLobbyCoroutine() {
            yield return new WaitForSeconds(EndSequenceDelaySeconds);
            DoReturnToLobby();
        }

        private void DoReturnToLobby() {
            FeralCore.Deactivate();
            ThePitState.ResetMatchState();
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                p.PlayableChampion?.Stats?.ClearActualEffects();
            }
            GameManager.Instance.RPC_Handle_ReturnToLobby(runIsWin: true, isFromEndScreen: true);
            Destroy(gameObject);
        }

        // ── HUD combat timer ─────────────────────────────────────────────────────

        // DifficultyManager only ticks CombatTimePrecise when EnemySpawnManager.IsActive,
        // which we suppress in ThePit. Drive it ourselves once the grace period ends.
        internal static void OnDifficultyFixedUpdate(DifficultyManager dm) {
            if (!ThePitState.CombatStarted || ThePitState.MatchEnded || dm.Runner?.IsServer != true) { return; }
            if (ThePitPatch.CombatTimePreciseSetter == null || ThePitPatch.CombatTimeInSecSetter == null) { return; }
            float precise = Mathf.Max(0f, dm.CombatTimePrecise - dm.Runner.DeltaTime);
            ThePitPatch.CombatTimePreciseSetter.Invoke(dm, new object[] { precise });
            ThePitPatch.CombatTimeInSecSetter.Invoke(dm, new object[] { (int)Mathf.Ceil(precise) });
        }

        // ── Respawn ──────────────────────────────────────────────────────────────

        private IEnumerator RespawnCoroutine(int victimActorId) {
            yield return new WaitForSeconds(RespawnDelaySeconds);
            if (ThePitState.MatchEnded) { yield break; }

            Player target = null;
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                if (p.PlayableChampion?.Stats?.ActorID == victimActorId) {
                    target = p;
                    break;
                }
            }
            if (target?.PlayableChampion == null) { yield break; }

            var champ = target.PlayableChampion;
            ThePitPatch.HealthInjurySetter?.Invoke(champ.Stats.Health, new object[] { 0f });
            champ.Stats.Health.Resurrect(100f);
            champ.Stats.Movement?.ResetRooted();
            GameManager.Instance.GetLevelManager()?.InitPlayerCharacterAtSpawnPoint(target, onlyTeleport: true);
            var respawnDoorGo = GameObject.Find("DoorSpawnPoint");
            if (respawnDoorGo != null) { champ.LookToPosition(respawnDoorGo.transform.position); }
            ThePitPatch.LockChampionAbilitiesFor(champ, RespawnInvincibilitySeconds);

            float deadline = Time.time + RespawnInvincibilitySeconds;
            FeralCore.GrantRespawnInvincibility(victimActorId, RespawnInvincibilitySeconds);
            champ.Stats.Health.AllDamageDisabled = true;
            StartCoroutine(ClearInvincibilityCoroutine(victimActorId, deadline));
        }
    }
}
