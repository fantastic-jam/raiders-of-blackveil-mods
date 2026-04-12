using System.Collections;
using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Perk;
using RR.Game.Stats;
using ThePit.FeralEngine;

namespace ThePit.FeralEngine.Abilities {
    // AreaCharacterSelector drives perk targeting (BlazeDevastation, Daggers, Bleeding Axes, etc.).
    //
    // Enemy mode (_actOnEnemies = true): SelectCandidates only iterates AllEnemies — misses champions.
    //   Fix: postfix calls CheckCandidate (via reflection) for each living opponent champion (except self).
    //
    // Ally mode (_actOnEnemies = false): SelectCandidates iterates GetPlayers — finds all champions.
    //   Fix: postfix strips all non-owner champions so ally perks (healing orbs, shields, regen)
    //   only affect the caster. TODO: revisit when a team system is introduced.
    internal static class AreaCharacterSelectorPatch {
        private static FieldInfo _actOnEnemiesField;
        private static FieldInfo _candidatesField;
        private static MethodInfo _checkCandidateMethod;
        private static MethodInfo _ownerStatGetter;

        internal static void Apply(Harmony harmony) {
            var selectorType = typeof(AreaCharacterSelector);

            _actOnEnemiesField = AccessTools.Field(selectorType, "_actOnEnemies");
            _candidatesField = AccessTools.Field(selectorType, "_candidates");
            _checkCandidateMethod = AccessTools.Method(selectorType, "CheckCandidate",
                new[] { typeof(StatsManager), typeof(bool), typeof(StatsManager) });
            _ownerStatGetter = AccessTools.PropertyGetter(selectorType, "OwnerStat");

            if (_actOnEnemiesField == null || _candidatesField == null || _checkCandidateMethod == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: AreaCharacterSelector fields not found — perk PvP targeting inactive.");
                return;
            }

            var selectCandidates = AccessTools.Method(selectorType, "SelectCandidates",
                new[] { typeof(StatsManager) });
            if (selectCandidates == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: AreaCharacterSelector.SelectCandidates not found — perk PvP targeting inactive.");
                return;
            }
            harmony.Patch(selectCandidates,
                postfix: new HarmonyMethod(typeof(AreaCharacterSelectorPatch), nameof(SelectCandidatesPostfix)));
        }

        private static void SelectCandidatesPostfix(AreaCharacterSelector __instance, StatsManager temporaryOwner) {
            if (__instance.Runner?.IsServer != true) { return; }

            bool actOnEnemies = (bool)_actOnEnemiesField.GetValue(__instance);
            var ownerStat = _ownerStatGetter?.Invoke(__instance, null) as StatsManager;
            if (ownerStat == null) { return; }

            if (actOnEnemies) {
                // Enemy mode: vanilla only iterates AllEnemies — misses player champions.
                // Add each living, non-invincible opponent so area-selector perks
                // (offensive perks, healing orbs on hit, etc.) can target them.
                foreach (var player in PlayerManager.Instance.GetPlayers()) {
                    var champ = player.PlayableChampion;
                    if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                    if (champ.Stats.ActorID == ownerStat.ActorID) { continue; }
                    if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

                    // CheckCandidate does the radius check and adds to _candidates if valid.
                    _checkCandidateMethod.Invoke(__instance, new object[] { champ.Stats, true, temporaryOwner });
                }
            } else {
                // Ally mode: vanilla finds all player champions, then _chooseOwnerLast removes the
                // owner when there are multiple candidates (i.e. enemy players found via Player layer).
                // Our strip-after approach would then remove those enemies too, leaving an empty list.
                // Fix: clear all candidates and let CheckCandidate evaluate just the owner directly.
                // If _ignoreAuraOwner is set the owner is correctly rejected; otherwise they're added
                // with a proper radius check. TODO: revisit when a team system is introduced.
                var candidates = _candidatesField.GetValue(__instance) as IList;
                if (candidates == null) { return; }

                candidates.Clear();
                _checkCandidateMethod.Invoke(__instance, new object[] { ownerStat, true, temporaryOwner });
            }
        }
    }
}
