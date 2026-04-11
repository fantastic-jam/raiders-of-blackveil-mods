using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // When a ProjectileCaster is PvP-expanded (_excludeCasterLayer = false), the normal
    // self-skip only checks the root gameObject and misses child colliders activated by
    // perk shield effects. Fix: before each FixedUpdateNetwork tick, mark every collider
    // in the caster's own hierarchy as "seen at tick-1" in the _projectileHits dicts.
    // The loop's existing duplicate-suppression (tick - value == 1 → continue) then
    // skips all own-hierarchy hits without touching the transpiled code.
    internal static class ProjectileCasterSelfSkipPatch {
        private static readonly FieldInfo _projectileHitsField =
            AccessTools.Field(typeof(ProjectileCaster), "_projectileHits");

        internal static void Apply(Harmony harmony) {
            if (_projectileHitsField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ProjectileCaster._projectileHits not found — self-skip fix inactive.");
                return;
            }
            var fixedUpdate = AccessTools.Method(typeof(ProjectileCaster), "FixedUpdateNetwork");
            if (fixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ProjectileCaster.FixedUpdateNetwork not found — self-skip fix inactive.");
                return;
            }
            harmony.Patch(fixedUpdate,
                prefix: new HarmonyMethod(typeof(ProjectileCasterSelfSkipPatch), nameof(FixedUpdateNetworkPrefix)));
        }

        private static void FixedUpdateNetworkPrefix(ProjectileCaster __instance) {
            PreMarkOwnHierarchy(__instance);
        }

        private static void PreMarkOwnHierarchy(ProjectileCaster caster) {
            if (!ThePitState.IsDraftMode) { return; }
            if (!caster.Object.HasStateAuthority) { return; }
            if (ProjectileCasterExpander.ExcludeCasterField == null) { return; }
            // Only applies when _excludeCasterLayer = false (PvP-expanded casters).
            if ((bool)ProjectileCasterExpander.ExcludeCasterField.GetValue(caster)) { return; }

            var owner = caster.OwnerCharacter;
            if (owner == null) { return; }

            var hits = _projectileHitsField.GetValue(caster) as Dictionary<GameObject, int>[];
            if (hits == null) { return; }

            int prevTick = caster.Runner.Tick - 1;
            foreach (var col in owner.GetComponentsInChildren<Collider>()) {
                if (col == null) { continue; }
                foreach (var dict in hits) {
                    if (dict != null) { dict[col.gameObject] = prevTick; }
                }
            }
        }
    }
}
