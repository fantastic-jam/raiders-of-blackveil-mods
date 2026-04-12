using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Beatrice's Entangling Roots fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    // ApplyRoot prefix: expanding the caster to the Player layer lets the projectile hit Beatrice
    // herself. TakeBasicDamage is already blocked globally, but GetRooted runs before it — so we
    // guard ApplyRoot against self-hits and invincible targets explicitly.
    internal static class BeatriceEntanglingRootsPatch {
        private static readonly ConditionalWeakTable<BeatriceEntanglingRootAbility, PvpBeatriceEntanglingRootsAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBeatriceEntanglingRootsAbility.Init();

            if (!ProjectileCasterExpander.IsReady) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ProjectileCasterExpander not ready — Entangling Roots PvP inactive.");
                return;
            }

            if (PvpBeatriceEntanglingRootsAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility._projectileCaster not found — Entangling Roots PvP inactive.");
                return;
            }

            var spawned = AccessTools.Method(typeof(BeatriceEntanglingRootAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility.Spawned not found — Entangling Roots PvP inactive.");
                return;
            }
            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(SpawnedPostfix)));

            var applyRoot = AccessTools.Method(typeof(BeatriceEntanglingRootAbility), "ApplyRoot");
            if (applyRoot == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility.ApplyRoot not found — self-root on launch not blocked.");
                return;
            }
            harmony.Patch(applyRoot, prefix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(ApplyRootPrefix)));
        }

        internal static void ExpandAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceEntanglingRootAbility>()) {
                _proxies.GetValue(a, inst => new PvpBeatriceEntanglingRootsAbility(inst)).Expand();
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceEntanglingRootAbility>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(BeatriceEntanglingRootAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBeatriceEntanglingRootsAbility(__instance));
        }

        private static bool ApplyRootPrefix(BeatriceEntanglingRootAbility __instance, Collider targetCol) =>
            PvpBeatriceEntanglingRootsAbility.ShouldApplyRoot(__instance, targetCol);
    }
}
