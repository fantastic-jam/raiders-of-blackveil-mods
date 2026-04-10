using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Perk;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    // AreaCharacterSelector drives perk targeting (BlazeDevastation, Daggers, Bleeding Axes, etc.).
    //
    // Enemy mode (_actOnEnemies = true): SelectCandidates only iterates AllEnemies — misses champions.
    //   Fix: postfix calls CheckCandidate (via reflection) for each living opponent champion.
    //
    // Ally mode (_actOnEnemies = false): SelectCandidates already iterates GetPlayers — would buff
    //   ALL champions (health regen, shields, etc.).
    //   Fix: postfix strips non-owner champions from _candidates.
    internal static class AreaCharacterSelectorPatch {
        private static FieldInfo _actOnEnemiesField;
        private static FieldInfo _candidatesField;
        private static FieldInfo _candidateActorField;
        private static MethodInfo _checkCandidateMethod;
        private static MethodInfo _ownerStatGetter;
        private static Type _candidateType;

        internal static void Apply(Harmony harmony) {
            var selectorType = typeof(AreaCharacterSelector);

            _actOnEnemiesField = AccessTools.Field(selectorType, "_actOnEnemies");
            _candidatesField = AccessTools.Field(selectorType, "_candidates");
            _checkCandidateMethod = AccessTools.Method(selectorType, "CheckCandidate",
                new[] { typeof(StatsManager), typeof(bool), typeof(StatsManager) });
            _ownerStatGetter = AccessTools.PropertyGetter(selectorType, "OwnerStat");

            _candidateType = selectorType.GetNestedType("Candidate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (_candidateType != null) {
                _candidateActorField = AccessTools.Field(_candidateType, "actor");
            }

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
            if (!ThePitState.IsAttackPossible) { return; }
            if (__instance.Runner?.IsServer != true) { return; }

            bool actOnEnemies = (bool)_actOnEnemiesField.GetValue(__instance);
            var ownerStat = _ownerStatGetter?.Invoke(__instance, null) as StatsManager;
            if (ownerStat == null) { return; }

            if (actOnEnemies) {
                // Enemy mode: add champions as targets so perks (Daggers, Axes, Devastation) hit them.
                foreach (var player in PlayerManager.Instance.GetPlayers()) {
                    var champ = player.PlayableChampion;
                    if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                    if (champ.Stats.ActorID == ownerStat.ActorID) { continue; }
                    if (ThePitState.IsPlayerInvincible(champ.Stats.ActorID)) { continue; }

                    // CheckCandidate does the radius check and adds to _candidates if valid.
                    _checkCandidateMethod.Invoke(__instance, new object[] { champ.Stats, true, temporaryOwner });
                }
            } else {
                // Ally mode: strip non-owner champions so health/shields only go to the caster.
                if (_candidatesField == null || _candidateActorField == null) { return; }
                var candidates = _candidatesField.GetValue(__instance) as IList;
                if (candidates == null) { return; }

                for (int i = candidates.Count - 1; i >= 0; i--) {
                    var actor = _candidateActorField.GetValue(candidates[i]) as StatsManager;
                    if (actor != null && actor.IsChampion && actor.ActorID != ownerStat.ActorID) {
                        candidates.RemoveAt(i);
                    }
                }
            }
        }
    }
}
