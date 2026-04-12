using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoStampedePatch {
        private static readonly ConditionalWeakTable<RhinoStampedeAbility, PvpRhinoStampedeAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpRhinoStampedeAbility.Init();

            var spawned = AccessTools.Method(typeof(RhinoStampedeAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility.Spawned not found — Stampede PvP inactive.");
                return;
            }

            var detect = AccessTools.Method(typeof(RhinoStampedeAbility), "DetectEnemiesToGrab");
            if (detect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility.DetectEnemiesToGrab not found — Stampede PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(RhinoStampedePatch), nameof(SpawnedPostfix)));
            harmony.Patch(detect, postfix: new HarmonyMethod(typeof(RhinoStampedePatch), nameof(DetectEnemiesToGrabPostfix)));
        }

        // Seed proxies for ability instances that were already spawned before FeralCore activated.
        internal static void SeedAllProxies() {
            foreach (var a in Object.FindObjectsOfType<RhinoStampedeAbility>()) {
                _proxies.GetValue(a, inst => new PvpRhinoStampedeAbility(inst));
            }
        }

        private static void SpawnedPostfix(RhinoStampedeAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpRhinoStampedeAbility(__instance));
        }

        private static void DetectEnemiesToGrabPostfix(RhinoStampedeAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DetectEnemies(); }
        }
    }
}
