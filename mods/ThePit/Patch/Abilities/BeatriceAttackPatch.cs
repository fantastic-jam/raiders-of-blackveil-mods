using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Beatrice's attack fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    // Self-damage is blocked globally by ThePitPatch.TakeBasicDamagePrefix.
    internal static class BeatriceAttackPatch {
        private static readonly ConditionalWeakTable<BeatriceAttackAbility, PvpBeatriceAttackAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (!ProjectileCasterExpander.IsReady || PvpBeatriceAttackAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceAttackAbility/_projectileCaster fields not found — Beatrice attack PvP inactive.");
                return;
            }
            var spawned = AccessTools.Method(typeof(BeatriceAttackAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceAttackAbility.Spawned not found — Beatrice attack PvP inactive.");
                return;
            }
            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BeatriceAttackPatch), nameof(SpawnedPostfix)));
        }

        internal static void ExpandAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceAttackAbility>()) {
                _sidecars.GetValue(a, inst => new PvpBeatriceAttackAbility(inst)).Expand();
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceAttackAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void SpawnedPostfix(BeatriceAttackAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBeatriceAttackAbility(inst)).TryExpand();
    }
}
