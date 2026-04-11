using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpRhinoShieldsUpAbility {
        internal static readonly FieldInfo BaseDamageField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "baseDamage");
        internal static readonly FieldInfo DealDamageAngleField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "dealDamageAngle");
        internal static readonly FieldInfo PushStrengthField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "pushStrength");

        private readonly RhinoShieldsUpAbility _inst;

        internal PvpRhinoShieldsUpAbility(RhinoShieldsUpAbility inst) { _inst = inst; }

        internal void HitEnemies() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.dealDamageCollider == null || BaseDamageField == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(_inst.dealDamageCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = (DamageDescriptor)BaseDamageField.GetValue(_inst);
            float angle = DealDamageAngleField != null ? (float)DealDamageAngleField.GetValue(_inst) : 75f;
            float cosAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
            var forward = _inst.transform.forward;
            var origin = _inst.transform.position;

            float pushStr = 0f;
            if (PushStrengthField != null) {
                var pct = PushStrengthField.GetValue(_inst);
                var valProp = pct?.GetType().GetProperty("Value");
                if (valProp != null) { pushStr = (float)valProp.GetValue(pct); }
            }

            bool anyHit = false;
            foreach (var target in hits) {
                var dir = target.transform.position - origin;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f) { continue; }
                var dirN = dir.normalized;
                if (Vector3.Dot(forward, dirN) <= cosAngle) { continue; }

                if (pushStr > 0f && target.CanBePushed && target.Character != null) {
                    target.Character.AddPushForce(dirN * pushStr);
                }
                target.TakeBasicDamage(dmg, self,
                    PvpDetector.AttackDir(_inst, target),
                    _inst.ConnectedUserAction, _inst.ImpactEffects);
                anyHit = true;
            }

            if (anyHit) { PvpDetector.ToggleHasHit(_inst); }
        }
    }
}
