using System.Collections;
using System.Reflection;
using HarmonyLib;
using RR.Game;
using RR.Game.Perk;
using RR.Game.Stats;

namespace RaiderRoughPatches {

    // Fixes AreaCharacterSelector._chooseOwnerLast removing the caster from
    // the candidate pool entirely when other allies are present.
    //
    // Root cause: SelectCandidates (line 1360) calls RemoveAll(owner) when
    // candidates.Count > 1 and _chooseOwnerLast=true. The intent is "prefer
    // allies, fall back to owner", but the implementation discards the owner
    // outright. DoAreaSelection then breaks after a single SelectCandidates
    // pass (_multipleSelectionEnabled=false), so the owner is never revisited.
    //
    // Fix: postfix on DoAreaSelection. After allies are selected and the perk
    // effect is already applied to them, check whether the owner was excluded.
    // If so, apply the perk func to the owner directly.

    internal static class BarrierSelfGrantFix {
        private static FieldInfo _targetsField;
        private static FieldInfo _chooseOwnerLastField;
        private static FieldInfo _actOnEnemiesField;
        private static FieldInfo _selectionModeField;
        private static FieldInfo _perkFuncSelectorIndexField;
        private static FieldInfo _candidateActorField;

        internal static void Init() {
            var areaType = typeof(AreaCharacterSelector);

            _targetsField = AccessTools.Field(areaType, "_targets");
            if (_targetsField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector._targets not found — barrier self-grant fix inactive.");
            }

            _chooseOwnerLastField = AccessTools.Field(areaType, "_chooseOwnerLast");
            if (_chooseOwnerLastField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector._chooseOwnerLast not found — barrier self-grant fix inactive.");
            }

            _actOnEnemiesField = AccessTools.Field(areaType, "_actOnEnemies");
            if (_actOnEnemiesField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector._actOnEnemies not found — barrier self-grant fix inactive.");
            }

            _selectionModeField = AccessTools.Field(areaType, "_selectionMode");
            if (_selectionModeField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector._selectionMode not found — barrier self-grant fix inactive.");
            }

            _perkFuncSelectorIndexField = AccessTools.Field(areaType, "_perkFuncSelectorIndex");
            if (_perkFuncSelectorIndexField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector._perkFuncSelectorIndex not found — barrier self-grant fix inactive.");
            }

            var candidateType = areaType.GetNestedType("Candidate", BindingFlags.NonPublic);
            if (candidateType == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector.Candidate type not found — barrier self-grant fix inactive.");
                return;
            }

            _candidateActorField = AccessTools.Field(candidateType, "actor");
            if (_candidateActorField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: AreaCharacterSelector.Candidate.actor not found — barrier self-grant fix inactive.");
            }
        }

        internal static bool IsReady =>
            _targetsField != null &&
            _chooseOwnerLastField != null &&
            _actOnEnemiesField != null &&
            _selectionModeField != null &&
            _perkFuncSelectorIndexField != null &&
            _candidateActorField != null;

        internal static void OnDoAreaSelectionDone(AreaCharacterSelector __instance) {
            if (!IsReady) { return; }
            if (!(bool)_chooseOwnerLastField.GetValue(__instance)) { return; }
            if ((bool)_actOnEnemiesField.GetValue(__instance)) { return; }

            var selectionMode = (PerkFunctionality.AreaSelectionMode)_selectionModeField.GetValue(__instance);
            if (selectionMode == PerkFunctionality.AreaSelectionMode.All) { return; }

            var targets = (IList)_targetsField.GetValue(__instance);
            if (targets.Count == 0) { return; }

            StatsManager owner = __instance.OwnerStat;
            if (owner == null || !owner.IsAlive) { return; }

            foreach (object t in targets) {
                if ((StatsManager)_candidateActorField.GetValue(t) == owner) { return; }
            }

            PerkFunctionality perkFunc = __instance.PerkFunc;
            if (perkFunc == null || owner.PerkHandler == null) { return; }

            byte selectorIndex = (byte)_perkFuncSelectorIndexField.GetValue(__instance);
            owner.PerkHandler.ActivatePerkFuncOnTarget(
                perkFunc, selectorIndex, owner,
                new TriggerParams(owner), __instance.transform, 0f, __instance);
        }
    }
}
