using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal static class RhinoShieldsUpPatch {
        private static FieldInfo _baseDamageField;
        private static FieldInfo _dealDamageAngleField;
        private static FieldInfo _pushStrengthField;

        internal static void Apply(Harmony harmony) {
            _baseDamageField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "baseDamage");
            _dealDamageAngleField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "dealDamageAngle");
            _pushStrengthField = AccessTools.Field(typeof(RhinoShieldsUpAbility), "pushStrength");

            if (_baseDamageField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.baseDamage not found — Shields Up PvP inactive.");
            }

            var hitEnemies = AccessTools.Method(typeof(RhinoShieldsUpAbility), "HitEnemies",
                new[] { typeof(float) });
            if (hitEnemies == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.HitEnemies not found — Shields Up PvP inactive.");
                return;
            }
            harmony.Patch(hitEnemies,
                postfix: new HarmonyMethod(typeof(RhinoShieldsUpPatch), nameof(HitEnemiesPostfix)));
        }

        private static void HitEnemiesPostfix(RhinoShieldsUpAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (__instance.dealDamageCollider == null || _baseDamageField == null) { return; }

            var self = __instance.Stats;
            var hits = PvpDetector.Overlap(__instance.dealDamageCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            // The original already set baseDamage.additionalFlatDamage before the loop.
            // Reading it here captures the absorbed-damage bonus.
            var dmg = (DamageDescriptor)_baseDamageField.GetValue(__instance);

            float angle = _dealDamageAngleField != null
                ? (float)_dealDamageAngleField.GetValue(__instance)
                : 75f;
            float cosAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
            var forward = __instance.transform.forward;
            var origin = __instance.transform.position;

            float pushStr = 0f;
            if (_pushStrengthField != null) {
                // pushStrength is a Percentage struct with a .Value property
                var pct = _pushStrengthField.GetValue(__instance);
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
                    PvpDetector.AttackDir(__instance, target),
                    __instance.ConnectedUserAction, __instance.ImpactEffects);
                anyHit = true;
            }

            if (anyHit) {
                PvpDetector.ToggleHasHit(__instance);
            }
        }
    }
}
