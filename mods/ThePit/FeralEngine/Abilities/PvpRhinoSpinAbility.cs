using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpRhinoSpinAbility {
        internal static readonly FieldInfo DamagePerCycleField = AccessTools.Field(typeof(RhinoSpinAbility), "damagePerCycle");
        internal static readonly FieldInfo TotalHitListField = AccessTools.Field(typeof(RhinoSpinAbility), "_totalHitList");

        private readonly RhinoSpinAbility _inst;

        internal PvpRhinoSpinAbility(RhinoSpinAbility inst) { _inst = inst; }

        internal void DoHit() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.hitCollider == null || DamagePerCycleField == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(_inst.hitCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = (DamageDescriptor)DamagePerCycleField.GetValue(_inst);
            var totalHitList = TotalHitListField?.GetValue(_inst) as List<StatsManager>;

            foreach (var target in hits) {
                target.TakeBasicDamage(dmg, self,
                    PvpDetector.AttackDir(_inst, target),
                    _inst.ConnectedUserAction, _inst.SpinImpactEffect);
                if (totalHitList != null && !totalHitList.Contains(target)) {
                    totalHitList.Add(target);
                }
            }
            PvpDetector.ToggleHasHit(_inst);
        }
    }
}
