using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Beatrice's Lotus Flower fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    internal static class BeatriceLotusFlowerPatch {
        private static readonly ConditionalWeakTable<BeatriceLotusFlowerAbility, PvpBeatriceLotusFlowerAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (!ProjectileCasterExpander.IsReady || PvpBeatriceLotusFlowerAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceLotusFlowerAbility/_projectileCaster fields not found — Lotus Flower PvP inactive.");
                return;
            }
            var spawned = AccessTools.Method(typeof(BeatriceLotusFlowerAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceLotusFlowerAbility.Spawned not found — Lotus Flower PvP inactive.");
                return;
            }
            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BeatriceLotusFlowerPatch), nameof(SpawnedPostfix)));
        }

        internal static void ExpandAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceLotusFlowerAbility>()) {
                _sidecars.GetValue(a, inst => new PvpBeatriceLotusFlowerAbility(inst)).Expand();
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceLotusFlowerAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void SpawnedPostfix(BeatriceLotusFlowerAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBeatriceLotusFlowerAbility(inst)).TryExpand();
    }
}
