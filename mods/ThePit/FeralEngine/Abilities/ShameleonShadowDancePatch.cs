using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Proxy pattern: LetsDance is replaced in PvP draft mode; vanilla runs outside PvP.
    // Spawned is postfixed to register the per-instance proxy (Fusion reuses CLR objects).
    internal static class ShameleonShadowDancePatch {
        private static readonly ConditionalWeakTable<ShameleonShadowDanceAbility, PvpShameleonShadowDanceAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpShameleonShadowDanceAbility.Init();

            var spawned = AccessTools.Method(typeof(ShameleonShadowDanceAbility), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.Spawned not found — Shadow Dance proxy inactive.");
            } else {
                harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ShameleonShadowDancePatch), nameof(SpawnedPostfix)));
            }

            var letsDance = AccessTools.Method(typeof(ShameleonShadowDanceAbility), "LetsDance");
            if (letsDance == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.LetsDance not found — Shadow Dance PvP inactive.");
                return;
            }
            harmony.Patch(letsDance, prefix: new HarmonyMethod(typeof(ShameleonShadowDancePatch), nameof(LetsDancePrefix)));

            // Backfill proxies for instances that were already spawned before Apply() ran
            // (Spawned() fires at match-start upgrades, before FeralCore patches are applied).
            foreach (var a in Object.FindObjectsOfType<ShameleonShadowDanceAbility>()) {
                InitProxy(a);
            }
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<ShameleonShadowDanceAbility>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void InitProxy(ShameleonShadowDanceAbility inst) {
            _proxies.Remove(inst);
            _proxies.Add(inst, new PvpShameleonShadowDanceAbility(inst));
        }

        private static void SpawnedPostfix(ShameleonShadowDanceAbility __instance) {
            InitProxy(__instance);
        }

        private static bool LetsDancePrefix(ShameleonShadowDanceAbility __instance, ref bool __result) {
            if (!ThePitState.IsDraftMode) { return true; }
            if (!_proxies.TryGetValue(__instance, out var proxy)) { return true; }
            return proxy.LetsDance(ref __result);
        }
    }
}
