using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Beatrice's Entangling Roots fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    // ApplyRoot prefix: expanding the caster to the Player layer lets the projectile hit Beatrice
    // herself. TakeBasicDamage is already blocked globally, but GetRooted runs before it — so we
    // guard ApplyRoot against self-hits and invincible targets explicitly.
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

            var applyRoot = AccessTools.Method(typeof(BeatriceEntanglingRootAbility), "ApplyRoot");
            if (applyRoot != null) {
                harmony.Patch(applyRoot, prefix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(ApplyRootPrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility.ApplyRoot not found — self-root on launch not blocked.");
            }
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

        // Block ApplyRoot when the projectile hits Beatrice herself or an invincible champion.
        // Also blocks when AllDamageDisabled is true (covers the one-frame gap before grace
        // invincibility is granted where the caster is already expanded but FeralCore hasn't
        // registered the respawn invincibility yet).
        // WitheredSeed hits are always allowed through (the seed revive logic must not be skipped).
        private static bool ApplyRootPrefix(BeatriceEntanglingRootAbility __instance, Collider targetCol) {
            if (targetCol.CompareTag("WitheredSeed")) { return true; }
            if (!targetCol.TryGetComponent<StatsManager>(out var stats) || !stats.IsChampion) { return true; }
            if (!ThePitState.IsDraftMode) { return false; }
            var caster = __instance.Stats;
            if (caster != null && stats.ActorID == caster.ActorID) { return false; }
            if (FeralCore.IsRespawnInvincible(stats.ActorID)) { return false; }
            if (caster != null && FeralCore.IsRespawnInvincible(caster.ActorID)) { return false; }
            if (stats.Health != null && stats.Health.AllDamageDisabled) { return false; }
            return true;
        }
    }
}
