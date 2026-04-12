using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // The original HitEnemiesInArch uses _hitDetector.DoDetection() (AllTargets) which
    // misses the Player layer.  We add champion detection on top via a postfix.
    //
    // The original Aim() only looks at NetworkEnemyBase.AllEnemies, so the plant never
    // turns toward champions and never starts an attack.  We add a postfix to also
    // consider champions as targets.
    internal static class ManEaterPlantBrainPatch {
        private static readonly ConditionalWeakTable<ManEaterPlantBrain, PvpManEaterPlantBrain> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpManEaterPlantBrain.Init();

            var spawned = AccessTools.Method(typeof(ManEaterPlantBrain), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.Spawned not found — Man-Eater Plant PvP inactive.");
                return;
            }

            var aim = AccessTools.Method(typeof(ManEaterPlantBrain), "Aim");
            if (aim == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.Aim not found — Man-Eater Plant won't target champions.");
            }

            var hitArch = AccessTools.Method(typeof(ManEaterPlantBrain), "HitEnemiesInArch");
            if (hitArch == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.HitEnemiesInArch not found — Man-Eater Plant PvP inactive.");
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ManEaterPlantBrainPatch), nameof(SpawnedPostfix)));
            if (aim != null) {
                harmony.Patch(aim, postfix: new HarmonyMethod(typeof(ManEaterPlantBrainPatch), nameof(AimPostfix)));
            }
            if (hitArch != null) {
                harmony.Patch(hitArch, postfix: new HarmonyMethod(typeof(ManEaterPlantBrainPatch), nameof(HitEnemiesInArchPostfix)));
            }
        }

        private static void SpawnedPostfix(ManEaterPlantBrain __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpManEaterPlantBrain(__instance));
        }

        private static void AimPostfix(ManEaterPlantBrain __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.Aim(); }
        }

        private static void HitEnemiesInArchPostfix(ManEaterPlantBrain __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.HitEnemiesInArch(); }
        }
    }
}
