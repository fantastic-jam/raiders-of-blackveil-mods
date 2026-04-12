using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Beatrice's attack fires projectiles via ProjectileCaster. Same fix as BlazeAttackPatch.
    // Self-damage is blocked globally by ThePitPatch.TakeBasicDamagePrefix.
    internal static class BeatriceAttackPatch {
        private static readonly ConditionalWeakTable<BeatriceAttackAbility, PvpBeatriceAttackAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBeatriceAttackAbility.Init();

            if (!ProjectileCasterExpander.IsReady) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ProjectileCasterExpander not ready — Beatrice attack PvP inactive.");
                return;
            }

            if (PvpBeatriceAttackAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceAttackAbility._projectileCaster not found — Beatrice attack PvP inactive.");
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
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Expand(); }
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BeatriceAttackAbility>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(BeatriceAttackAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBeatriceAttackAbility(__instance));
        }
    }
}
