using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class SunStrikeAreaPatch {
        private static readonly ConditionalWeakTable<SunStrikeArea, PvpSunStrikeArea> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(SunStrikeArea), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: SunStrikeArea.Spawned not found — Sun Strike PvP inactive.");
                return;
            }

            var dmgCheck = AccessTools.Method(typeof(SunStrikeArea), "DamageCheck");
            if (dmgCheck == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: SunStrikeArea.DamageCheck not found — Sun Strike PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(SunStrikeAreaPatch), nameof(SpawnedPostfix)));
            harmony.Patch(dmgCheck, postfix: new HarmonyMethod(typeof(SunStrikeAreaPatch), nameof(DamageCheckPostfix)));
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<SunStrikeArea>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(SunStrikeArea __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpSunStrikeArea(__instance));
        }

        private static void DamageCheckPostfix(SunStrikeArea __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DamageCheck(); }
        }
    }
}
