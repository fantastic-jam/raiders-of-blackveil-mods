using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    // NetworkChampionMinion.SelectEnemyTarget and GeneralAttackConditionsOk use
    // NetworkEnemyBase.AllEnemies for targeting — champions are never found.
    // Postfix on both to also consider champions as targets, same pattern as
    // ManEaterPlantBrainPatch.AimPostfix.
    internal static class ChampionMinionPatch {
        private static readonly ConditionalWeakTable<NetworkChampionMinion, PvpChampionMinion> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpChampionMinion.TargetCharacterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion._targetCharacter not found — minion champion targeting inactive.");
            }

            var selectEnemy = AccessTools.Method(typeof(NetworkChampionMinion), "SelectEnemyTarget");
            if (selectEnemy != null) {
                harmony.Patch(selectEnemy, postfix: new HarmonyMethod(typeof(ChampionMinionPatch), nameof(SelectEnemyTargetPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion.SelectEnemyTarget not found — minion champion targeting inactive.");
            }

            var generalConditions = AccessTools.Method(typeof(NetworkChampionMinion), "GeneralAttackConditionsOk");
            if (generalConditions != null) {
                harmony.Patch(generalConditions, postfix: new HarmonyMethod(typeof(ChampionMinionPatch), nameof(GeneralAttackConditionsPostfix)));
            }
        }

        private static void SelectEnemyTargetPostfix(NetworkChampionMinion __instance, ref bool __result) {
            if (__result || !ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            __result = _sidecars.GetValue(__instance, inst => new PvpChampionMinion(inst)).SelectEnemyTarget();
        }

        private static void GeneralAttackConditionsPostfix(NetworkChampionMinion __instance, ref bool __result) {
            if (__result || !ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            __result = _sidecars.GetValue(__instance, inst => new PvpChampionMinion(inst)).GeneralAttackConditions();
        }
    }
}
