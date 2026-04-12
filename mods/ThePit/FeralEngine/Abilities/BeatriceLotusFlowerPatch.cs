using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Beatrice's Lotus Flower fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    internal static class BeatriceLotusFlowerPatch {
        private static readonly ConditionalWeakTable<BeatriceLotusFlowerAbility, PvpBeatriceLotusFlowerAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBeatriceLotusFlowerAbility.Init();

            if (!ProjectileCasterExpander.IsReady) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ProjectileCasterExpander not ready — Lotus Flower PvP inactive.");
                return;
            }

            if (PvpBeatriceLotusFlowerAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceLotusFlowerAbility._projectileCaster not found — Lotus Flower PvP inactive.");
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
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Expand(); }
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceLotusFlowerAbility>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(BeatriceLotusFlowerAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBeatriceLotusFlowerAbility(__instance));
        }
    }
}
