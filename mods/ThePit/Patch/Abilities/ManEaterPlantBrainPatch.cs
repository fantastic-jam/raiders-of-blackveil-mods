using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    // The original HitEnemiesInArch uses _hitDetector.DoDetection() (AllTargets) which
    // misses the Player layer.  We add champion detection on top via a postfix.
    //
    // The original Aim() only looks at NetworkEnemyBase.AllEnemies, so the plant never
    // turns toward champions and never starts an attack.  We add a postfix to also
    // consider champions as targets.
    internal static class ManEaterPlantBrainPatch {
        private static readonly ConditionalWeakTable<ManEaterPlantBrain, PvpManEaterPlantBrain> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpManEaterPlantBrain.HasTargetField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.hasTarget not found — Man-Eater Plant won't target champions.");
            }

            var aim = AccessTools.Method(typeof(ManEaterPlantBrain), "Aim");
            if (aim == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.Aim not found — Man-Eater Plant won't target champions.");
            } else {
                harmony.Patch(aim, postfix: new HarmonyMethod(typeof(ManEaterPlantBrainPatch), nameof(AimPostfix)));
            }

            var hitArch = AccessTools.Method(typeof(ManEaterPlantBrain), "HitEnemiesInArch");
            if (hitArch == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.HitEnemiesInArch not found — Man-Eater Plant PvP inactive.");
                return;
            }
            harmony.Patch(hitArch, postfix: new HarmonyMethod(typeof(ManEaterPlantBrainPatch), nameof(HitEnemiesInArchPostfix)));
        }

        private static void AimPostfix(ManEaterPlantBrain __instance) {
            if (!ThePitState.IsAttackPossible) { return; }
            _sidecars.GetValue(__instance, inst => new PvpManEaterPlantBrain(inst)).Aim();
        }

        private static void HitEnemiesInArchPostfix(ManEaterPlantBrain __instance) {
            if (!ThePitState.IsAttackPossible) { return; }
            _sidecars.GetValue(__instance, inst => new PvpManEaterPlantBrain(inst)).HitEnemiesInArch();
        }
    }
}
