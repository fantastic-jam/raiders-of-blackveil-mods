using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoSpinPatch {
        private static readonly ConditionalWeakTable<RhinoSpinAbility, PvpRhinoSpinAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpRhinoSpinAbility.Init();

            var spawned = AccessTools.Method(typeof(RhinoSpinAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.Spawned not found — Spin PvP inactive.");
                return;
            }

            var doHit = AccessTools.Method(typeof(RhinoSpinAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.DoHit not found — Spin PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(RhinoSpinPatch), nameof(SpawnedPostfix)));
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(RhinoSpinPatch), nameof(DoHitPostfix)));
        }

        // Seed proxies for ability instances that were already spawned before FeralCore activated.
        internal static void SeedAllProxies() {
            foreach (var a in Object.FindObjectsOfType<RhinoSpinAbility>()) {
                _proxies.GetValue(a, inst => new PvpRhinoSpinAbility(inst));
            }
        }

        private static void SpawnedPostfix(RhinoSpinAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpRhinoSpinAbility(__instance));
        }

        private static void DoHitPostfix(RhinoSpinAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DoHit(); }
        }
    }
}
