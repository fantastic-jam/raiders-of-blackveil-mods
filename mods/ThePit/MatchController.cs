using System.Collections;
using RR;
using RR.Game;
using RR.Level;
using ThePit.Patch;
using ThePit.Patch.Abilities;
using UnityEngine;

namespace ThePit {
    internal class MatchController : MonoBehaviour {
        private static float MatchDurationSeconds =>
            ThePitMod.CfgMatchDurationSeconds?.Value ?? 600f; // original: 600 (10 minutes)
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
            TeleportAllToCenter();
            _instance.StartCoroutine(_instance.ArenaGraceCoroutine());
            _instance.StartCoroutine(_instance.MatchTimerCoroutine());
        }

        private static void TeleportAllToCenter() {
            // DoorSpawnPoint is where the floor pit door lives in the SlashBash arena.
            // Fall back to averaging player spawn points if the object is absent.
            Vector3 center;
            var doorGo = GameObject.Find("DoorSpawnPoint");
            if (doorGo != null) {
                center = doorGo.transform.position;
            } else {
                var sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < 3; i++) {
                    var sp = GameObject.Find($"PlayerSpawnPoints/PlayerSpawnPoint{i}");
                    if (sp != null) { sum += sp.transform.position; count++; }
                }
                if (count == 0) { return; }
                center = sum / count;
            }

            var players = PlayerManager.Instance.GetPlayers();
            int n = players.Count;
            if (n == 0) { return; }

            const float radius = 2.5f;
            for (int i = 0; i < n; i++) {
                var player = players[i];
                if (player.PlayableChampion == null) { continue; }
                float angle = Mathf.PI * 2f / n * i;
                var offset = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                var pos = center + offset;
                var lookDir = center - pos;
                lookDir.y = 0f;
                var rot = lookDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookDir) : Quaternion.identity;
                player.PlayableChampion.TeleportTo(pos, rot);
            }
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
            float deadline = Time.time + ArenaGracePeriodSeconds;
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                var champ = p.PlayableChampion;
                if (champ == null) { continue; }
                ThePitState.InvincibleUntil[champ.Stats.ActorID] = deadline;
                StartCoroutine(ClearInvincibilityCoroutine(champ.Stats.ActorID, deadline));
            }
            yield break;
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
            ThePitState.IsActive = false;
            ThePitState.ResetMatchState();
            GameManager.Instance.RPC_Handle_ReturnToLobby(runIsWin: true, isFromEndScreen: true);
            Destroy(gameObject);
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
            ThePitPatch.LockChampionAbilitiesFor(champ, RespawnInvincibilitySeconds);

            float deadline = Time.time + RespawnInvincibilitySeconds;
            ThePitState.InvincibleUntil[victimActorId] = deadline;
            champ.Stats.Health.AllDamageDisabled = true;
            StartCoroutine(ClearInvincibilityCoroutine(victimActorId, deadline));
        }
    }
}
