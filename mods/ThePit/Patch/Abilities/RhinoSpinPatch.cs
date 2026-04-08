using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    internal static class RhinoSpinPatch {
        private static FieldInfo _damagePerCycleField;
        private static FieldInfo _totalHitListField;

        internal static void Apply(Harmony harmony) {
            _damagePerCycleField = AccessTools.Field(typeof(RhinoSpinAbility), "damagePerCycle");
            _totalHitListField = AccessTools.Field(typeof(RhinoSpinAbility), "_totalHitList");

            if (_damagePerCycleField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.damagePerCycle not found — Spin PvP inactive.");
            }

            var doHit = AccessTools.Method(typeof(RhinoSpinAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.DoHit not found — Spin PvP inactive.");
                return;
            }
            harmony.Patch(doHit,
                postfix: new HarmonyMethod(typeof(RhinoSpinPatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(RhinoSpinAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (__instance.hitCollider == null || _damagePerCycleField == null) { return; }

            var self = __instance.Stats;
            var hits = PvpDetector.Overlap(__instance.hitCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = (DamageDescriptor)_damagePerCycleField.GetValue(__instance);
            var totalHitList = _totalHitListField?.GetValue(__instance) as List<StatsManager>;

            foreach (var target in hits) {
                target.TakeBasicDamage(dmg, self,
                    PvpDetector.AttackDir(__instance, target),
                    __instance.ConnectedUserAction, __instance.SpinImpactEffect);

                // Track for event triggers (same list the original uses for events).
                if (totalHitList != null && !totalHitList.Contains(target)) {
                    totalHitList.Add(target);
                }
            }
            PvpDetector.ToggleHasHit(__instance);
        }
    }
}
