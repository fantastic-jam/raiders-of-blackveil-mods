using System.Collections;
using RR;
using RR.Game;
using RR.Game.Perk;
using RR.Level;
using UnityEngine;

namespace ThePit {
    internal class PerkDripController : MonoBehaviour {
        private const float SceneInitDelaySecs = 2f;
        private const int MaxPerksPerPlayer = 20;
        private const int InitialPerkCount = 3;
        private const int MaxXpLevel = 20;

        private static float PerkIntervalSeconds =>
            ThePitMod.CfgPerkIntervalSeconds?.Value ?? 15f;   // original: 30
        private static float XpTickIntervalSeconds =>
            ThePitMod.CfgXpTickIntervalSeconds?.Value ?? 23f; // original: 45

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

        private IEnumerator PerkCoroutine() {
            // Wait for RewardManager.SceneInit and spawn teleport before dropping.
            yield return new WaitForSeconds(SceneInitDelaySecs);
            GrantPerksToAllPlayers(InitialPerkCount, Rarity.Common);
            // Open door immediately — players take it when ready (acts as ready vote).
            DoorManager.Instance?.Activate(string.Empty);
            // Wait until arena is entered before starting the periodic drip.
            yield return new WaitUntil(() => ThePitState.ArenaEntered || ThePitState.MatchEnded);
            while (ThePitState.IsDraftMode && !ThePitState.MatchEnded) {
                yield return new WaitForSeconds(PerkIntervalSeconds);
                _arenaRound++;
                // All players receive the same rarity tier per tick.
                GrantPerksToAllPlayers(1, PickRarity(_arenaRound));
            }
            Destroy(gameObject);
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
            while (ThePitState.IsDraftMode && !ThePitState.MatchEnded) {
                yield return new WaitForSeconds(XpTickIntervalSeconds);
                GrantXpTickToAllPlayers();
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

        private static void GrantXpTickToAllPlayers() {
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

                    int newXP = limits[currentLevel];
                    int points = rdb.GetXPUpgradePoints(currentXP, newXP);
                    champ.XP.Amount = newXP;
                    champ.XP.AbilityPoints += points;
                }
                catch (System.InvalidOperationException) {
                    // PlayerManager not yet Spawned — skip this tick entirely.
                    return;
                }
            }
        }
    }
}
