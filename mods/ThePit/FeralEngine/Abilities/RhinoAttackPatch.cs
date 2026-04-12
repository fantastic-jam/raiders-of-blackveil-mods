using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoAttackPatch {
        private static readonly ConditionalWeakTable<RhinoAttackAbility, PvpRhinoAttackAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(RhinoAttackAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoAttackAbility.Spawned not found — Rhino attack PvP inactive.");
                return;
            }

            var doHit = AccessTools.Method(typeof(RhinoAttackAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoAttackAbility.DoHit not found — Rhino attack PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(RhinoAttackPatch), nameof(SpawnedPostfix)));
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(RhinoAttackPatch), nameof(DoHitPostfix)));
        }

        // Seed proxies for ability instances that were already spawned before FeralCore activated.
        internal static void SeedAllProxies() {
            foreach (var a in Object.FindObjectsOfType<RhinoAttackAbility>()) {
                _proxies.GetValue(a, inst => new PvpRhinoAttackAbility(inst));
            }
        }

        private static void SpawnedPostfix(RhinoAttackAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpRhinoAttackAbility(__instance));
        }

        private static void DoHitPostfix(RhinoAttackAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DoHit(); }
        }
    }
}
