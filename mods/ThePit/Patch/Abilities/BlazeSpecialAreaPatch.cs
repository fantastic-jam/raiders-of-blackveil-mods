using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Perk;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    // Restricts Blaze's heat aura critical-chance buff to the caster only.
    // In PvP, all three champions enter range → all three would get the crit buff.
    // We replace UpdateAuraEffect to rebuild the AlliesInside list with just the caster.
    internal static class BlazeSpecialAreaPatch {
        private static FieldInfo _alliesInsideField;
        private static FieldInfo _tempStatsListField;

        internal static void Apply(Harmony harmony) {
            _alliesInsideField = AccessTools.Field(typeof(BlazeSpecialArea), "AlliesInside");
            _tempStatsListField = AccessTools.Field(typeof(BlazeSpecialArea), "_tempStatsList");

            if (_alliesInsideField == null || _tempStatsListField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea fields not found — Heat Aura PvP fix inactive.");
            }

            var updateAura = AccessTools.Method(typeof(BlazeSpecialArea), "UpdateAuraEffect",
                new[] { typeof(bool) });
            if (updateAura == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.UpdateAuraEffect not found — Heat Aura PvP fix inactive.");
                return;
            }
            harmony.Patch(updateAura,
                prefix: new HarmonyMethod(typeof(BlazeSpecialAreaPatch), nameof(UpdateAuraEffectPrefix)));
        }

        private static bool UpdateAuraEffectPrefix(BlazeSpecialArea __instance, bool checkWhoIsInside) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return true; }
            if (_alliesInsideField == null || _tempStatsListField == null) { return true; }

            var casterStats = __instance.areaCaster?.Stats;
            if (casterStats == null) { return true; } // no caster info, run original

            var alliesInside = _alliesInsideField.GetValue(__instance) as List<StatsManager>;
            var tempStats = _tempStatsListField.GetValue(__instance) as List<StatsManager>;
            if (alliesInside == null || tempStats == null) { return true; }

            // Rebuild tempStats with only the caster if they are within radius.
            tempStats.Clear();
            if (checkWhoIsInside && casterStats.IsAlive) {
                var diff = casterStats.transform.position - __instance.transform.position;
                diff.y = 0f;
                if (diff.magnitude <= __instance.ActRadius) {
                    tempStats.Add(casterStats);
                }
            }

            // Remove buff from anyone leaving (was in AlliesInside, not in tempStats).
            foreach (var ally in alliesInside) {
                if (!tempStats.Contains(ally)) {
                    ally.ClearTemporaryModifiedProperty(
                        Property.CriticalStrikeChance, __instance.criticalChanceIncrementPCT);
                }
            }

            // Add buff to anyone newly entering (in tempStats, not in AlliesInside).
            foreach (var sm in tempStats) {
                if (!alliesInside.Contains(sm)) {
                    sm.ModifyPropertyForFrames(
                        Property.CriticalStrikeChance, __instance.criticalChanceIncrementPCT, 999999);
                }
            }

            // Sync AlliesInside = tempStats.
            alliesInside.Clear();
            alliesInside.AddRange(tempStats);

            return false; // skip original
        }
    }
}
