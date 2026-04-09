using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Blaze's attack fires projectiles via ProjectileCaster. The caster's layer masks
    // are set in the Unity Editor and don't include the Player layer, so projectiles
    // pass through other champions.
    //
    // Fix: sidecar expands masks at arena entry via TryExpand()/Expand().
    // Self-damage is blocked globally by ThePitPatch.TakeBasicDamagePrefix.
    // On match reset masks are restored via ResetAllCasters().
    internal static class BlazeAttackPatch {
        private static readonly ConditionalWeakTable<BlazeAttackAbility, PvpBlazeAttackAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (!ProjectileCasterExpander.IsReady || PvpBlazeAttackAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeAttackAbility/_projectileCaster fields not found — Blaze attack PvP inactive.");
                return;
            }
            var spawned = AccessTools.Method(typeof(BlazeAttackAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeAttackAbility.Spawned not found — Blaze attack PvP inactive.");
                return;
            }
            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BlazeAttackPatch), nameof(SpawnedPostfix)));
        }

        internal static void ExpandAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BlazeAttackAbility>()) {
                _sidecars.GetValue(a, inst => new PvpBlazeAttackAbility(inst)).Expand();
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BlazeAttackAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void SpawnedPostfix(BlazeAttackAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBlazeAttackAbility(inst)).TryExpand();
    }
}
