using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class SunStrikeAreaPatch {
        private static readonly ConditionalWeakTable<SunStrikeArea, PvpSunStrikeArea> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var dmgCheck = AccessTools.Method(typeof(SunStrikeArea), "DamageCheck");
            if (dmgCheck == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: SunStrikeArea.DamageCheck not found — Sun Strike PvP inactive.");
                return;
            }
            harmony.Patch(dmgCheck, postfix: new HarmonyMethod(typeof(SunStrikeAreaPatch), nameof(DamageCheckPostfix)));
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<SunStrikeArea>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void DamageCheckPostfix(SunStrikeArea __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpSunStrikeArea(inst)).DamageCheck();
    }
}
