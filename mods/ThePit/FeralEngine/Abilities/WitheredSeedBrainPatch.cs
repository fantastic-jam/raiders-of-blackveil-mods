using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // WitheredSeedBrain.Aim() only scans NetworkEnemyBase.AllEnemies — empty in PvP.
    // Postfix: if no enemy was found, scan for the nearest living opponent champion and
    // rotate toward them (mirrors ManEaterPlantBrainPatch.AimPostfix).
    // Spawned postfix: expand the projectile caster so seed shots reach the Player layer.
    internal static class WitheredSeedBrainPatch {
        private static readonly ConditionalWeakTable<WitheredSeedBrain, PvpWitheredSeedBrain> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpWitheredSeedBrain.Init();

            if (PvpWitheredSeedBrain.CasterField == null || !ProjectileCasterExpander.IsReady) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain._projectileCaster fields not found — seed turret PvP inactive.");
                return;
            }

            var spawned = AccessTools.Method(typeof(WitheredSeedBrain), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain.Spawned not found — seed turret PvP inactive.");
                return;
            }

            var aim = AccessTools.Method(typeof(WitheredSeedBrain), "Aim");
            if (aim == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain.Aim not found — seed turret won't target champions.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(WitheredSeedBrainPatch), nameof(SpawnedPostfix)));
            harmony.Patch(aim, postfix: new HarmonyMethod(typeof(WitheredSeedBrainPatch), nameof(AimPostfix)));
        }

        internal static void ResetAllCasters() {
            foreach (var inst in Object.FindObjectsOfType<WitheredSeedBrain>()) {
                if (_proxies.TryGetValue(inst, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(WitheredSeedBrain __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpWitheredSeedBrain(__instance));
        }

        private static void AimPostfix(WitheredSeedBrain __instance, ref bool __result) {
            if (__result) { return; }
            if (_proxies.TryGetValue(__instance, out var proxy)) { __result = proxy.Aim(); }
        }
    }
}
