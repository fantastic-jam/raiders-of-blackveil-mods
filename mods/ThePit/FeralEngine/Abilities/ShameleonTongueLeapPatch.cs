using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal static class ShameleonTongueLeapPatch {
        private static readonly ConditionalWeakTable<ShameleonTongueLeapAbility, PvpShameleonTongueLeapAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var fun = AccessTools.Method(typeof(ShameleonTongueLeapAbility), "FixedUpdateNetwork");
            if (fun == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonTongueLeapAbility.FixedUpdateNetwork not found — Tongue Leap PvP inactive.");
                return;
            }
            harmony.Patch(fun,
                prefix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(FunPrefix)),
                postfix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(FunPostfix)));
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<ShameleonTongueLeapAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void FunPrefix(ShameleonTongueLeapAbility __instance) {
            _sidecars.GetValue(__instance, inst => new PvpShameleonTongueLeapAbility(inst)).Prefix();
        }

        private static void FunPostfix(ShameleonTongueLeapAbility __instance) {
            _sidecars.GetValue(__instance, inst => new PvpShameleonTongueLeapAbility(inst)).Postfix();
        }
    }
}
