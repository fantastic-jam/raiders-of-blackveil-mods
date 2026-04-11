using System.Collections;
using RR;
using RR.Game;
using RR.Game.Character;
using RR.Game.Perk;
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

        private const float ArenaGraceSpeedBoostPct = 100f; // +100% on top of base = 2× total during arena grace
        private const float RespawnSpeedBoostPct = 200f;   // +200% on top of base = 3× total during respawn grace

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
            ThePitState.CachedDamageReductionFactor = ThePitState.ResolvedDamageReductionFactor;
            // Champions spawned in the lobby before ArenaEntered was true — expand now.
            AbilityPatch.ExpandAllCasters();
            _instance.StartCoroutine(_instance.ArenaGraceCoroutine());
            _instance.StartCoroutine(_instance.FaceTowardDoorCoroutine());
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
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                var champ = p.PlayableChampion;
                if (champ == null) { continue; }
                champ.Stats.Movement?.ResetRooted();
                champ.Stats.ModifyPropertyForFrames(Property.MovementSpeed, ArenaGraceSpeedBoostPct, 999999);
                FeralCore.GrantRespawnInvincibility(champ.Stats.ActorID, ArenaGracePeriodSeconds);
                StartCoroutine(ClearInvincibilityCoroutine(champ.Stats.ActorID, deadline, speedBoostPct: ArenaGraceSpeedBoostPct));
            }
            // Abilities aren't fully initialised on the first frame — delay like FaceTowardDoorCoroutine.
            yield return new WaitForSeconds(0.1f);
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                var champ = p.PlayableChampion;
                if (champ == null) { continue; }
                ThePitPatch.LockChampionAbilitiesFor(champ, deadline - Time.time);
            }
            yield return new WaitUntil(() => Time.time >= deadline);
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                var champ = p.PlayableChampion;
                if (champ == null) { continue; }
                ThePitPatch.UnlockChampionAbilities(champ);
            }
            var dm = DifficultyManager.Instance;
            if (dm != null) {
                ThePitPatch.CombatTimePreciseSetter?.Invoke(dm, new object[] { MatchDurationSeconds });
                ThePitPatch.CombatTimeInSecSetter?.Invoke(dm, new object[] { (int)Mathf.Ceil(MatchDurationSeconds) });
            }
            ThePitState.CombatStarted = true;
        }

        // 1 s after arena entry the bossintro cutscene is running; LookToPosition sticks at that point.
        // Using 1 s (instead of 0.1 s) ensures the cutscene is still active on second run when
        // scene assets are already cached and init completes faster.
        private IEnumerator FaceTowardDoorCoroutine() {
            yield return new WaitForSeconds(1f);
            var doorGo = GameObject.Find("DoorSpawnPoint");
            if (doorGo == null) { yield break; }
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                p.PlayableChampion?.LookToPosition(doorGo.transform.position);
            }
        }

        // Clears AllDamageDisabled only if our deadline is still the current one.
        // If a new respawn fired in between, InvincibleUntil was updated and we bail.
        // withImmune/withInvisible/withSpeedBoost: only true for respawn grace (paired with AddImmune/AddInvisible/ModifyProperty).
        private IEnumerator ClearInvincibilityCoroutine(int actorId, float deadline, bool withImmune = false, bool withInvisible = false, float speedBoostPct = 0f) {
            yield return new WaitUntil(() => Time.time >= deadline);
            if (!FeralCore.TryGetInvincibilityDeadline(actorId, out float current) || current != deadline) {
                yield break;
            }
            FeralCore.RemoveInvincibility(actorId);
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                if (p.PlayableChampion?.Stats?.ActorID == actorId) {
                    p.PlayableChampion.Stats.Health.AllDamageDisabled = false;
                    if (withInvisible) { p.PlayableChampion.Stats.Health.RemoveInvisible(); }
                    if (withImmune) { p.PlayableChampion.Stats.Health.RemoveImmune(); }
                    if (speedBoostPct > 0f) { p.PlayableChampion.Stats.ClearTemporaryModifiedProperty(Property.MovementSpeed, speedBoostPct); }
                    break;
                }
            }
        }

        // ── Match timer ──────────────────────────────────────────────────────────

        private IEnumerator MatchTimerCoroutine() {
            yield return new WaitForSeconds(ArenaGracePeriodSeconds);
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

            // Grant invincibility to all players — match is over, no more combat.
            // AddImmune only for the winner (losers get it from RespawnCoroutine).
            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ == null) { continue; }
                FeralCore.GrantRespawnInvincibility(champ.Stats.ActorID, 300f);
                champ.Stats.Health.AllDamageDisabled = true;
                if (champ.Stats.Health.IsAlive) { champ.Stats.Health.AddImmune(); }
            }

            // Open the arena door so the winner can exit (normally opens on enemies cleared).
            DoorManager.Instance?.Activate();

            // Resurrect dead players after 5 s so they can reach the door and participate in the vote.
            StartCoroutine(ResurrectForLobbyCoroutine());
        }

        private IEnumerator ResurrectForLobbyCoroutine() {
            yield return new WaitForSeconds(5f);
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                var champ = p.PlayableChampion;
                if (champ == null || champ.Stats.Health.IsAlive) { continue; }
                ThePitPatch.HealthInjurySetter?.Invoke(champ.Stats.Health, new object[] { 0f });
                champ.Stats.Health.Resurrect(100f);
                champ.Stats.Health.AllDamageDisabled = true;
                champ.Stats.Health.AddImmune();
                champ.Stats.Movement?.ResetRooted();
                GameManager.Instance.GetLevelManager()?.InitPlayerCharacterAtSpawnPoint(p, onlyTeleport: true);
            }
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

        private void DoReturnToLobby() {
            FeralCore.Deactivate();
            ThePitState.ResetMatchState();
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                p.PlayableChampion?.Stats?.ClearActualEffects();
            }
            GameManager.Instance.RPC_Handle_ReturnToLobby(runIsWin: true, isFromEndScreen: true);
            _instance = null;
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
            champ.Stats.Health.AddInvisible();
            champ.Stats.Health.AddImmune();
            champ.Stats.ModifyPropertyForFrames(Property.MovementSpeed, RespawnSpeedBoostPct, 999999);
            FeralCore.GrantRespawnInvincibility(victimActorId, RespawnInvincibilitySeconds);
            champ.Stats.Health.AllDamageDisabled = true;
            StartCoroutine(ClearInvincibilityCoroutine(victimActorId, deadline, withImmune: true, withInvisible: true, speedBoostPct: RespawnSpeedBoostPct));
        }
    }
}
