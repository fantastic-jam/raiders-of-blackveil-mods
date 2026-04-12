using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpRhinoSpinAbility {
        private static FieldInfo _damagePerCycleField;
        private static FieldInfo _totalHitListField;

        private readonly RhinoSpinAbility _inst;

        internal static void Init() {
            _damagePerCycleField = AccessTools.Field(typeof(RhinoSpinAbility), "damagePerCycle");
            if (_damagePerCycleField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.damagePerCycle not found — Spin damage inactive.");
            }

            _totalHitListField = AccessTools.Field(typeof(RhinoSpinAbility), "_totalHitList");
            if (_totalHitListField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility._totalHitList not found — Spin hit list tracking inactive.");
            }
        }

        internal PvpRhinoSpinAbility(RhinoSpinAbility inst) { _inst = inst; }

        internal void DoHit() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.hitCollider == null) { return; }
            if (_damagePerCycleField == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(_inst.hitCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = (DamageDescriptor)_damagePerCycleField.GetValue(_inst);
            var totalHitList = _totalHitListField?.GetValue(_inst) as List<StatsManager>;

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
