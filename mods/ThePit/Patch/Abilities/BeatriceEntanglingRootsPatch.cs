using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Beatrice's Entangling Roots fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    internal static class BeatriceEntanglingRootsPatch {
        private static readonly ConditionalWeakTable<BeatriceEntanglingRootAbility, PvpBeatriceEntanglingRootsAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (!ProjectileCasterExpander.IsReady || PvpBeatriceEntanglingRootsAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility/_projectileCaster fields not found — Entangling Roots PvP inactive.");
                return;
            }
            var spawned = AccessTools.Method(typeof(BeatriceEntanglingRootAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility.Spawned not found — Entangling Roots PvP inactive.");
                return;
            }
            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(SpawnedPostfix)));
        }

        internal static void ExpandAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceEntanglingRootAbility>()) {
                _sidecars.GetValue(a, inst => new PvpBeatriceEntanglingRootsAbility(inst)).Expand();
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceEntanglingRootAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void SpawnedPostfix(BeatriceEntanglingRootAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBeatriceEntanglingRootsAbility(inst)).TryExpand();
    }
}
