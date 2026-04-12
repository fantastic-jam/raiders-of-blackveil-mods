using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoShieldsUpPatch {
        private static readonly ConditionalWeakTable<RhinoShieldsUpAbility, PvpRhinoShieldsUpAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpRhinoShieldsUpAbility.Init();

            var spawned = AccessTools.Method(typeof(RhinoShieldsUpAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.Spawned not found — Shields Up PvP inactive.");
                return;
            }

            var hitEnemies = AccessTools.Method(typeof(RhinoShieldsUpAbility), "HitEnemies", new[] { typeof(float) });
            if (hitEnemies == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.HitEnemies not found — Shields Up PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(RhinoShieldsUpPatch), nameof(SpawnedPostfix)));
            harmony.Patch(hitEnemies, postfix: new HarmonyMethod(typeof(RhinoShieldsUpPatch), nameof(HitEnemiesPostfix)));
        }

        // Seed proxies for ability instances that were already spawned before FeralCore activated.
        internal static void SeedAllProxies() {
            foreach (var a in Object.FindObjectsOfType<RhinoShieldsUpAbility>()) {
                _proxies.GetValue(a, inst => new PvpRhinoShieldsUpAbility(inst));
            }
        }

        private static void SpawnedPostfix(RhinoShieldsUpAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpRhinoShieldsUpAbility(__instance));
        }

        private static void HitEnemiesPostfix(RhinoShieldsUpAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.HitEnemies(); }
        }
    }
}
