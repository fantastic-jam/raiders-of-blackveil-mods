using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Beatrice's Entangling Roots fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    // ApplyRoot prefix: guards self-hits and invincible targets.
    // ApplyRoot postfix: registers the newly-rooted victim for death-cleanup.
    // FixedUpdateNetwork postfix: if the tracked victim dies, clears their root immediately.
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

            var fixedUpdate = AccessTools.Method(typeof(BeatriceEntanglingRootAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(FixedUpdateNetworkPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility.FixedUpdateNetwork not found — death-root cleanup inactive.");
            }

            var applyRoot = AccessTools.Method(typeof(BeatriceEntanglingRootAbility), "ApplyRoot");
            if (applyRoot == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility.ApplyRoot not found — self-root on launch not blocked.");
                return;
            }
            harmony.Patch(applyRoot, prefix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(ApplyRootPrefix)));
            harmony.Patch(applyRoot, postfix: new HarmonyMethod(typeof(BeatriceEntanglingRootsPatch), nameof(ApplyRootPostfix)));
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

        private static void FixedUpdateNetworkPostfix(BeatriceEntanglingRootAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnFixedUpdate(); }
        }

        private static bool ApplyRootPrefix(BeatriceEntanglingRootAbility __instance, Collider targetCol) {
            if (!_proxies.TryGetValue(__instance, out var proxy)) { return true; }
            return proxy.OnApplyRootPrefix(targetCol);
        }

        private static void ApplyRootPostfix(BeatriceEntanglingRootAbility __instance, Collider targetCol) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnApplyRootPostfix(targetCol); }
        }
    }
}
