using System.Collections;
using RR;
using RR.Game;
using RR.Game.Character;
using RR.Level;
using ThePit.Patch;
using ThePit.Patch.Abilities;
using UnityEngine;

namespace ThePit {
    internal class MatchController : MonoBehaviour {
        private static float MatchDurationSeconds =>
            ThePitState.MatchDurationSecondsOverride > 0f
                ? ThePitState.MatchDurationSecondsOverride
                : (ThePitMod.CfgMatchDurationSeconds?.Value ?? 600f);
        private const float ArenaGracePeriodSeconds = 5f;
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
            // Champions spawned in the lobby before ArenaEntered was true — expand now.
            BlazeAttackPatch.ExpandAllCasters();
            BeatriceAttackPatch.ExpandAllCasters();
            BeatriceEntanglingRootsPatch.ExpandAllCasters();
            BeatriceLotusFlowerPatch.ExpandAllCasters();
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
                ThePitState.InvincibleUntil[champ.Stats.ActorID] = deadline;
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
            if (!ThePitState.InvincibleUntil.TryGetValue(actorId, out float current) || current != deadline) {
                yield break;
            }
            ThePitState.InvincibleUntil.Remove(actorId);
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

            // Open the arena floor door as the visual match-over signal.
            DoorManager.Instance?.Activate(string.Empty);
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

        private IEnumerator ReturnToLobbyCoroutine() {
            yield return new WaitForSeconds(EndSequenceDelaySeconds);
            ThePitState.ResetMatchState();
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

        // ── Helpers ──────────────────────────────────────────────────────────────

        // Rotate the champion to face the arena center without touching spawn point transforms
        // (which would affect the camera rig).
        private static void FaceArenaCenter(NetworkChampionBase champ) {
            var doorGo = GameObject.Find("DoorSpawnPoint");
            if (doorGo == null) { return; }
            champ.LookToPosition(doorGo.transform.position);
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
            GameManager.Instance.GetLevelManager()?.InitPlayerCharacterAtSpawnPoint(target, onlyTeleport: true);
            FaceArenaCenter(champ);
            ThePitPatch.LockChampionAbilitiesFor(champ, RespawnInvincibilitySeconds);

            float deadline = Time.time + RespawnInvincibilitySeconds;
            ThePitState.InvincibleUntil[victimActorId] = deadline;
            champ.Stats.Health.AllDamageDisabled = true;
            StartCoroutine(ClearInvincibilityCoroutine(victimActorId, deadline));
        }
    }
}
