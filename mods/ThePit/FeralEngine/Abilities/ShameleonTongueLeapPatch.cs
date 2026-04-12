using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Augmentation pattern: vanilla FixedUpdateNetwork runs; we expand tongueHitMask in a
    // prefix so the original raycast can connect with champions, then apply PvP damage in the
    // postfix and restore the mask.
    internal static class ShameleonTongueLeapPatch {
        private static readonly ConditionalWeakTable<ShameleonTongueLeapAbility, PvpShameleonTongueLeapAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(ShameleonTongueLeapAbility), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonTongueLeapAbility.Spawned not found — Tongue Leap proxy inactive.");
            }

            var fixedUpdate = AccessTools.Method(typeof(ShameleonTongueLeapAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate,
                    prefix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(FixedUpdateNetworkPrefix)),
                    postfix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(FixedUpdateNetworkPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonTongueLeapAbility.FixedUpdateNetwork not found — Tongue Leap PvP inactive.");
            }

            // Backfill proxies for instances that were already spawned before Apply() ran
            // (Spawned() fires at match-start upgrades, before FeralCore patches are applied).
            foreach (var a in Object.FindObjectsOfType<ShameleonTongueLeapAbility>()) {
                InitProxy(a);
            }
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<ShameleonTongueLeapAbility>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void InitProxy(ShameleonTongueLeapAbility inst) {
            _proxies.Remove(inst);
            _proxies.Add(inst, new PvpShameleonTongueLeapAbility(inst));
        }

        private static void SpawnedPostfix(ShameleonTongueLeapAbility __instance) {
            InitProxy(__instance);
        }

        private static void FixedUpdateNetworkPrefix(ShameleonTongueLeapAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.Prefix(); }
        }

        private static void FixedUpdateNetworkPostfix(ShameleonTongueLeapAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.Postfix(); }
        }
    }
}
