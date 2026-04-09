using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal static class ShameleonShadowDancePatch {
        private static readonly ConditionalWeakTable<ShameleonShadowDanceAbility, PvpShameleonShadowDanceAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpShameleonShadowDanceAbility.DamagePerAttackField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.damagePerAttack not found — Shadow Dance PvP inactive.");
            }
            if (PvpShameleonShadowDanceAbility.SpawnedShadowCountField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility._spawnedShadowCount not found — Shadow Dance may not exit.");
            }

            var letsDance = AccessTools.Method(typeof(ShameleonShadowDanceAbility), "LetsDance");
            if (letsDance == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.LetsDance not found — Shadow Dance PvP inactive.");
                return;
            }
            harmony.Patch(letsDance, prefix: new HarmonyMethod(typeof(ShameleonShadowDancePatch), nameof(LetsDancePrefix)));
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<ShameleonShadowDanceAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static bool LetsDancePrefix(ShameleonShadowDanceAbility __instance, ref bool __result) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return true; }
            (bool skip, bool result) = _sidecars.GetValue(__instance, inst => new PvpShameleonShadowDanceAbility(inst)).LetsDance();
            if (!skip) { return true; }
            __result = result;
            return false;
        }
    }
}
