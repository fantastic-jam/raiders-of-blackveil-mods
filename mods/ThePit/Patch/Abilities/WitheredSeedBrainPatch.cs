using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // WitheredSeedBrain.Aim() only scans NetworkEnemyBase.AllEnemies — empty in PvP.
    // Postfix: if no enemy was found, scan for the nearest living opponent champion and
    // rotate toward them (mirrors ManEaterPlantBrainPatch.AimPostfix).
    // Spawned postfix: expand the projectile caster so seed shots reach the Player layer.
    internal static class WitheredSeedBrainPatch {
        private static readonly ConditionalWeakTable<WitheredSeedBrain, PvpWitheredSeedBrain> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpWitheredSeedBrain.HasTargetField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain._hasTarget not found — seed turret champion targeting inactive.");
            }
            if (PvpWitheredSeedBrain.CasterField == null || !ProjectileCasterExpander.IsReady) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain._projectileCaster fields not found — seed turret PvP inactive.");
                return;
            }

            var spawned = AccessTools.Method(typeof(WitheredSeedBrain), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(WitheredSeedBrainPatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain.Spawned not found — seed turret PvP inactive.");
                return;
            }

            var aim = AccessTools.Method(typeof(WitheredSeedBrain), "Aim");
            if (aim == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain.Aim not found — seed turret won't target champions.");
                return;
            }
            harmony.Patch(aim, postfix: new HarmonyMethod(typeof(WitheredSeedBrainPatch), nameof(AimPostfix)));
        }

        internal static void ResetAllCasters() {
            foreach (var s in Object.FindObjectsOfType<WitheredSeedBrain>()) {
                if (_sidecars.TryGetValue(s, out var pvp)) { pvp.Reset(); }
            }
        }

        private static void SpawnedPostfix(WitheredSeedBrain __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpWitheredSeedBrain(inst)).TryExpand();

        private static void AimPostfix(WitheredSeedBrain __instance, ref bool __result) {
            if (__result || !ThePitState.IsAttackPossible) { return; }
            __result = _sidecars.GetValue(__instance, inst => new PvpWitheredSeedBrain(inst)).Aim();
        }
    }
}
