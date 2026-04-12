using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Blaze's attack fires projectiles via ProjectileCaster. The caster's layer masks
    // are set in the Unity Editor and don't include the Player layer, so projectiles
    // pass through other champions.
    //
    // Fix: proxy expands masks at arena entry via Expand().
    // Self-damage is blocked globally by ThePitPatch.TakeBasicDamagePrefix.
    // On match reset masks are restored via ResetAllCasters().
    internal static class BlazeAttackPatch {
        private static readonly ConditionalWeakTable<BlazeAttackAbility, PvpBlazeAttackAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBlazeAttackAbility.Init();

            if (!ProjectileCasterExpander.IsReady) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ProjectileCasterExpander not ready — Blaze attack PvP inactive.");
                return;
            }

            if (PvpBlazeAttackAbility.CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeAttackAbility._projectileCaster not found — Blaze attack PvP inactive.");
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
                _proxies.GetValue(a, inst => new PvpBlazeAttackAbility(inst)).Expand();
            }
        }

        internal static void ResetAllCasters() {
            foreach (var a in Object.FindObjectsOfType<BlazeAttackAbility>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(BlazeAttackAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBlazeAttackAbility(__instance));
        }
    }
}
