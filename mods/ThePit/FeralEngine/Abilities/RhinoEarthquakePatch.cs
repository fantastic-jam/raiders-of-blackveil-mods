using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoEarthquakePatch {
        private static readonly ConditionalWeakTable<RhinoEarthquakeAbility, PvpRhinoEarthquakeAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpRhinoEarthquakeAbility.Init();

            var spawned = AccessTools.Method(typeof(RhinoEarthquakeAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility.Spawned not found — Earthquake PvP inactive.");
                return;
            }

            var fixedUpdate = AccessTools.Method(typeof(RhinoEarthquakeAbility), "FixedUpdateNetwork");
            if (fixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility.FixedUpdateNetwork not found — Earthquake PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(RhinoEarthquakePatch), nameof(SpawnedPostfix)));
            harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(RhinoEarthquakePatch), nameof(FixedUpdateNetworkPostfix)));
        }

        // Seed proxies for ability instances that were already spawned before FeralCore activated.
        internal static void SeedAllProxies() {
            foreach (var a in Object.FindObjectsOfType<RhinoEarthquakeAbility>()) {
                _proxies.GetValue(a, inst => new PvpRhinoEarthquakeAbility(inst));
            }
        }

        private static void SpawnedPostfix(RhinoEarthquakeAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpRhinoEarthquakeAbility(__instance));
        }

        private static void FixedUpdateNetworkPostfix(RhinoEarthquakeAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnFixedUpdate(); }
        }
    }
}
