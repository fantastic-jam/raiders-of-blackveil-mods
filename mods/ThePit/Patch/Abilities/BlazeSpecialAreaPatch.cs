using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Restricts Blaze's heat aura critical-chance buff to the caster only.
    // Also burns champion opponents inside the aura at the same rate as normal enemies.
    internal static class BlazeSpecialAreaPatch {
        private static readonly ConditionalWeakTable<BlazeSpecialArea, PvpBlazeSpecialArea> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpBlazeSpecialArea.AlliesInsideField == null || PvpBlazeSpecialArea.TempStatsListField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea fields not found — Heat Aura PvP fix inactive.");
            }

            var updateAura = AccessTools.Method(typeof(BlazeSpecialArea), "UpdateAuraEffect", new[] { typeof(bool) });
            if (updateAura == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.UpdateAuraEffect not found — Heat Aura PvP fix inactive.");
            } else {
                harmony.Patch(updateAura, prefix: new HarmonyMethod(typeof(BlazeSpecialAreaPatch), nameof(UpdateAuraEffectPrefix)));
            }

            var fixedUpdate = AccessTools.Method(typeof(BlazeSpecialArea), "FixedUpdateNetwork");
            if (fixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.FixedUpdateNetwork not found — champion burn inactive.");
            } else {
                harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(BlazeSpecialAreaPatch), nameof(FixedUpdateNetworkPostfix)));
            }
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<BlazeSpecialArea>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static bool UpdateAuraEffectPrefix(BlazeSpecialArea __instance, bool checkWhoIsInside) {
            if (!ThePitState.IsAttackPossible) { return true; }
            return _sidecars.GetValue(__instance, inst => new PvpBlazeSpecialArea(inst)).UpdateAuraEffect(checkWhoIsInside);
        }

        private static void FixedUpdateNetworkPostfix(BlazeSpecialArea __instance) {
            if (!ThePitState.IsAttackPossible) { return; }
            _sidecars.GetValue(__instance, inst => new PvpBlazeSpecialArea(inst)).OnFixedUpdate();
        }
    }
}
