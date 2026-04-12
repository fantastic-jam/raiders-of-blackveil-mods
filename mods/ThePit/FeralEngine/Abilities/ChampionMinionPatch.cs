using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Enemies;

namespace ThePit.FeralEngine.Abilities {
    // NetworkChampionMinion.SelectEnemyTarget and GeneralAttackConditionsOk use
    // NetworkEnemyBase.AllEnemies for targeting — champions are never found.
    // Postfix on both to also consider champions as targets, same pattern as
    // ManEaterPlantBrainPatch.AimPostfix.
    internal static class ChampionMinionPatch {
        private static readonly ConditionalWeakTable<NetworkChampionMinion, PvpChampionMinion> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpChampionMinion.Init();

            var spawned = AccessTools.Method(typeof(NetworkChampionMinion), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion.Spawned not found — minion champion targeting inactive.");
                return;
            }

            var selectEnemy = AccessTools.Method(typeof(NetworkChampionMinion), "SelectEnemyTarget");
            if (selectEnemy == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion.SelectEnemyTarget not found — minion champion targeting inactive.");
                return;
            }

            var generalConditions = AccessTools.Method(typeof(NetworkChampionMinion), "GeneralAttackConditionsOk");
            if (generalConditions == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion.GeneralAttackConditionsOk not found — minion general-attack PvP inactive.");
            }

            var onDead = AccessTools.Method(typeof(NetworkChampionMinion), "OnDead");
            if (onDead == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion.OnDead not found — minion OnDead guard inactive.");
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ChampionMinionPatch), nameof(SpawnedPostfix)));
            harmony.Patch(selectEnemy, postfix: new HarmonyMethod(typeof(ChampionMinionPatch), nameof(SelectEnemyTargetPostfix)));
            if (generalConditions != null) {
                harmony.Patch(generalConditions, postfix: new HarmonyMethod(typeof(ChampionMinionPatch), nameof(GeneralAttackConditionsPostfix)));
            }
            if (onDead != null) {
                harmony.Patch(onDead, prefix: new HarmonyMethod(typeof(ChampionMinionPatch), nameof(OnDeadPrefix)));
            }
        }

        private static void SpawnedPostfix(NetworkChampionMinion __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpChampionMinion(__instance));
        }

        private static void SelectEnemyTargetPostfix(NetworkChampionMinion __instance, ref bool __result) {
            if (__result) { return; }
            if (_proxies.TryGetValue(__instance, out var proxy)) { __result = proxy.SelectEnemyTarget(); }
        }

        private static void GeneralAttackConditionsPostfix(NetworkChampionMinion __instance, ref bool __result) {
            if (__result) { return; }
            if (_proxies.TryGetValue(__instance, out var proxy)) { __result = proxy.GeneralAttackConditions(); }
        }

        private static void OnDeadPrefix(NetworkChampionMinion __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.ClearNonEnemyTarget(); }
        }
    }
}
