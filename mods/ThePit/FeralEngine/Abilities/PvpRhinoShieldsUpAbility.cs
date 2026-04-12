using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpRhinoShieldsUpAbility {
        private static FieldInfo _baseDamageField;
        private static FieldInfo _dealDamageAngleField;
        private static FieldInfo _pushStrengthField;

        private readonly RhinoShieldsUpAbility _inst;

        internal static void Init() {
            _baseDamageField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "baseDamage");
            if (_baseDamageField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.baseDamage not found — Shields Up damage inactive.");
            }

            _dealDamageAngleField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "dealDamageAngle");
            if (_dealDamageAngleField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.dealDamageAngle not found — Shields Up angle will use fallback.");
            }

            _pushStrengthField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "pushStrength");
            if (_pushStrengthField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.pushStrength not found — Shields Up push inactive.");
            }
        }

        internal PvpRhinoShieldsUpAbility(RhinoShieldsUpAbility inst) { _inst = inst; }

        internal void HitEnemies() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.dealDamageCollider == null) { return; }
            if (_baseDamageField == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(_inst.dealDamageCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = (DamageDescriptor)_baseDamageField.GetValue(_inst);
            float angle = _dealDamageAngleField != null ? (float)_dealDamageAngleField.GetValue(_inst) : 75f;
            float cosAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
            var forward = _inst.transform.forward;
            var origin = _inst.transform.position;

            float pushStr = 0f;
            if (_pushStrengthField != null) {
                var pct = _pushStrengthField.GetValue(_inst);
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
