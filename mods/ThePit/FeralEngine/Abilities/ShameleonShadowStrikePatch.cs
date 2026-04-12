using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Augmentation pattern: vanilla DoHit runs for PvE; we layer PvP detection on top in a postfix.
    internal static class ShameleonShadowStrikePatch {
        private static readonly ConditionalWeakTable<ShameleonShadowStrikeAbility, PvpShameleonShadowStrikeAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(ShameleonShadowStrikeAbility), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ShameleonShadowStrikePatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowStrikeAbility.Spawned not found — Shadow Strike proxy inactive.");
            }

            var doHit = AccessTools.Method(typeof(ShameleonShadowStrikeAbility), "DoHit");
            if (doHit != null) {
                harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(ShameleonShadowStrikePatch), nameof(DoHitPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowStrikeAbility.DoHit not found — Shadow Strike PvP inactive.");
            }

            // Backfill proxies for instances that were already spawned before Apply() ran
            // (Spawned() fires at match-start upgrades, before FeralCore patches are applied).
            foreach (var a in Object.FindObjectsOfType<ShameleonShadowStrikeAbility>()) {
                InitProxy(a);
            }
        }

        private static void InitProxy(ShameleonShadowStrikeAbility inst) {
            _proxies.Remove(inst);
            _proxies.Add(inst, new PvpShameleonShadowStrikeAbility(inst));
        }

        private static void SpawnedPostfix(ShameleonShadowStrikeAbility __instance) {
            InitProxy(__instance);
        }

        private static void DoHitPostfix(ShameleonShadowStrikeAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DoHit(); }
        }
    }
}
