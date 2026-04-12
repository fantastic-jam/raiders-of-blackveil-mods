using System.Collections;
using RR;
using RR.Game;
using RR.Game.Perk;
using RR.Level;
using RR.Utility;
using UnityEngine;

namespace ThePit {
    internal class PerkDripController : MonoBehaviour {
        private const float SceneInitDelaySecs = 2f;
        private const int DefaultInitialChestRounds = 6;
        private const int MaxXpLevel = 20;

        private static int MaxPerksPerPlayer => ThePitMod.CfgMaxPerksPerPlayer?.Value ?? 30;

        private static int InitialChestRounds =>
            ThePitState.InitialChestRoundsOverride >= 0 ? ThePitState.InitialChestRoundsOverride : DefaultInitialChestRounds;

        private static float PerkIntervalSeconds =>
            (ThePitMod.CfgPerkIntervalSeconds?.Value ?? 30f) * ThePitState.DropIntervalMultiplier;

        private static float XpTickIntervalSeconds =>
            (ThePitMod.CfgXpTickIntervalSeconds?.Value ?? 45f) * ThePitState.DropIntervalMultiplier;

        private static readonly PlayerFilter[] _allFilters = {
            PlayerFilter.Player0, PlayerFilter.Player1, PlayerFilter.Player2
        };

        // Counts perk drip ticks after arena entry — used for rarity escalation.
        private int _arenaRound;

        internal static void StartDrip() {
            var go = new GameObject("ThePit_PerkDrip");
            Object.DontDestroyOnLoad(go);
            var ctrl = go.AddComponent<PerkDripController>();
            ctrl.StartCoroutine(ctrl.PerkCoroutine());
            ctrl.StartCoroutine(ctrl.XpCoroutine());
        }

        private bool _chestRoundDone;

        private IEnumerator PerkCoroutine() {
            // Wait for RewardManager.SceneInit and spawn teleport before starting chests.
            yield return new WaitForSeconds(SceneInitDelaySecs);
            // Grant health boosts for the starting level before the chest phase.
            GrantInitialHealthBoosts();
            // Run 6 sequential perk chest rounds; door opens only after all are done.
            yield return StartCoroutine(RunInitialChestRounds());
            DoorManager.Instance?.Activate(string.Empty);
            // Wait until arena is entered before starting the periodic drip.
            yield return new WaitUntil(() => ThePitState.ArenaEntered || ThePitState.MatchEnded);
            while (ThePitState.IsDraftMode && !ThePitState.MatchEnded) {
                _arenaRound++;
                // All players receive the same rarity tier per tick.
                GrantPerksToAllPlayers(1, PickRarity(_arenaRound));
                yield return new WaitForSeconds(PerkIntervalSeconds);
            }
            Destroy(gameObject);
        }

        private IEnumerator RunInitialChestRounds() {
            var shrine = ShrineHandler.Instance;
            if (shrine == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShrineHandler not found — granting perks directly.");
                GrantPerksToAllPlayers(InitialChestRounds, Rarity.Common);
                yield break;
            }

            ThePitState.ChestPhaseActive = true;
            var runner = PlayerManager.Instance.Runner;
            GameEvents.AddListener(
                gameObject,
                GameEvents.GetGameEvent("LevelEvent_ShrineFinished"),
                OnShrineFinished);

            for (int round = 0; round < InitialChestRounds; round++) {
                if (round > 0) {
                    // ShrineHandler._state stays Active after CheckFinished — only SceneExit
                    // resets it to Finished, which is the state Activate() requires.
                    shrine.SceneExit();
                    // Despawn any shrine items that weren't already auto-cleaned by the game.
                    foreach (var s in FindObjectsOfType<ShrineItem>()) {
                        if (s?.Object != null && s.Object.IsValid) {
                            runner.Despawn(s.Object);
                        }
                    }
                    yield return null; // Let despawn propagate.
                }

                _chestRoundDone = false;
                shrine.Activate(string.Empty, LevelType.None);
                yield return new WaitUntil(() => _chestRoundDone);
            }

            // Despawn last round's shrines.
            foreach (var s in FindObjectsOfType<ShrineItem>()) {
                if (s?.Object != null && s.Object.IsValid) {
                    runner.Despawn(s.Object);
                }
            }

            ThePitState.ChestPhaseActive = false;
        }

        private void OnShrineFinished() {
            _chestRoundDone = true;
        }

        // Rarity weights that escalate with each arena perk tick:
        //   rounds  1–3:  80% Common, 20% Rare
        //   rounds  4–6:  60% Common, 30% Rare, 10% Epic
        //   rounds  7–10: 40% Common, 35% Rare, 20% Epic,  5% Legendary
        //   rounds 11+:   25% Common, 35% Rare, 25% Epic, 15% Legendary
        private static Rarity PickRarity(int round) {
            float roll = Random.value;
            if (round <= 3) {
                return roll < 0.80f ? Rarity.Common : Rarity.Rare;
            }
            if (round <= 6) {
                if (roll < 0.60f) { return Rarity.Common; }
                if (roll < 0.90f) { return Rarity.Rare; }
                return Rarity.Epic;
            }
            if (round <= 10) {
                if (roll < 0.40f) { return Rarity.Common; }
                if (roll < 0.75f) { return Rarity.Rare; }
                if (roll < 0.95f) { return Rarity.Epic; }
                return Rarity.Legendary;
            }
            if (roll < 0.25f) { return Rarity.Common; }
            if (roll < 0.60f) { return Rarity.Rare; }
            if (roll < 0.85f) { return Rarity.Epic; }
            return Rarity.Legendary;
        }

        private IEnumerator XpCoroutine() {
            yield return new WaitUntil(() => ThePitState.ArenaEntered);
            var wait = new WaitForSeconds(1f);
            while (ThePitState.IsDraftMode && !ThePitState.MatchEnded) {
                GrantXpIncrement();
                yield return wait;
            }
        }

        private static void GrantInitialHealthBoosts() {
            var rdb = RewardDatabase.Instance;
            var healthPerk = rdb?.StatMaxHealth?.Perk;
            if (healthPerk == null) { return; }
            int boosts = ThePitState.InitialLevelOverride / 2;
            if (boosts <= 0) { return; }
            foreach (var filter in _allFilters) {
                var player = PlayerManager.Instance?.GetPlayerByPlayerFilter(filter);
                if (player?.PlayableChampion?.PerkHandler == null) { continue; }
                for (int i = 0; i < boosts; i++) {
                    player.PlayableChampion.PerkHandler.CollectPerkOnHost(healthPerk);
                }
            }
        }

        private static void GrantPerksToAllPlayers(int count, Rarity rarity) {
            var db = PerkDatabase.Instance;
            if (db == null) { return; }

            foreach (var filter in _allFilters) {
                var player = PlayerManager.Instance?.GetPlayerByPlayerFilter(filter);
                if (player?.PlayableChampion == null) { continue; }

                var perkHandler = player.PlayableChampion.PerkHandler;
                if (perkHandler == null) { continue; }

                int canGrant = Mathf.Min(count, MaxPerksPerPlayer - perkHandler.CollectedPerks.Count);
                if (canGrant <= 0) { continue; }

                var perks = db.GetRandomPerkAmount(canGrant, Category.None, rarity, filter, ignoreThesePerks: null);
                if (perks == null) { continue; }

                foreach (var perk in perks) {
                    perkHandler.CollectPerkOnHost(perk);
                }
            }
        }

        // Drips XP every second so the gauge fills smoothly.
        // Rate: one level fills over XpTickIntervalSeconds. Ability points are granted
        // at the moment a level boundary is crossed, not at the end of the interval.
        private static void GrantXpIncrement() {
            var rdb = RewardDatabase.Instance;
            if (rdb?.XPDescriptor == null) { return; }

            var limits = rdb.XPDescriptor.XPLimits;

            foreach (var filter in _allFilters) {
                try {
                    var champ = PlayerManager.Instance?.GetPlayerByPlayerFilter(filter)?.PlayableChampion;
                    if (champ == null) { continue; }

                    int currentXP = champ.XP.Amount;
                    int currentLevel = rdb.GetXPLevel(currentXP);
                    if (currentLevel >= MaxXpLevel || currentLevel >= limits.Count) { continue; }

                    // limits[currentLevel - 1] = XP at start of this level.
                    // limits[currentLevel]     = XP threshold to reach next level.
                    int levelStart = limits[currentLevel - 1];
                    int levelEnd = limits[currentLevel];
                    int levelRange = levelEnd - levelStart;

                    int xpGain = Mathf.Max(1, Mathf.RoundToInt((float)levelRange / XpTickIntervalSeconds));
                    int newXP = Mathf.Min(levelEnd, currentXP + xpGain);

                    if (newXP <= currentXP) { continue; }

                    // Grant ability points and health boosts the moment a level boundary is crossed.
                    if (rdb.GetXPLevel(newXP) > currentLevel) {
                        champ.XP.AbilityPoints += rdb.GetXPUpgradePoints(currentXP, newXP);
                        int newLevel = rdb.GetXPLevel(newXP);
                        if (newLevel % 2 == 0) {
                            var healthPerk = rdb.StatMaxHealth?.Perk;
                            if (healthPerk != null) { champ.PerkHandler?.CollectPerkOnHost(healthPerk); }
                        }
                    }

                    champ.XP.Amount = newXP;
                }
                catch (System.InvalidOperationException) {
                    // PlayerManager not yet Spawned — skip this tick entirely.
                    return;
                }
            }
        }
    }
}
