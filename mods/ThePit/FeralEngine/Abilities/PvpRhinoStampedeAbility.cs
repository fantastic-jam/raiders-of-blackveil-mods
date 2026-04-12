using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpRhinoStampedeAbility {
        private static FieldInfo _grabbedEnemiesField;
        private static FieldInfo _grabbedThanThrowedField;
        private static MethodInfo _grabActorMethod;

        private readonly RhinoStampedeAbility _inst;

        internal static void Init() {
            _grabbedEnemiesField = AccessTools.Field(typeof(RhinoStampedeAbility), "_grabbedEnemies");
            if (_grabbedEnemiesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility._grabbedEnemies not found — Stampede grab deduplication inactive.");
            }

            _grabbedThanThrowedField = AccessTools.Field(typeof(RhinoStampedeAbility), "_grabbedThanThrowedEnemies");
            if (_grabbedThanThrowedField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility._grabbedThanThrowedEnemies not found — Stampede throw deduplication inactive.");
            }

            _grabActorMethod = AccessTools.Method(typeof(RhinoStampedeAbility), "GrabActor", new[] { typeof(StatsManager) });
            if (_grabActorMethod == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility.GrabActor not found — Stampede grab inactive.");
            }
        }

        internal PvpRhinoStampedeAbility(RhinoStampedeAbility inst) { _inst = inst; }

        internal void DetectEnemies() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.grabCollider == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(_inst.grabCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var grabbed = _grabbedEnemiesField?.GetValue(_inst) as List<StatsManager>;
            var throwed = _grabbedThanThrowedField?.GetValue(_inst) as List<StatsManager>;

            foreach (var target in hits) {
                if (!target.IsAlive) { continue; }
                if (grabbed != null && grabbed.Contains(target)) { continue; }
                if (throwed != null && throwed.Contains(target)) { continue; }

                target.TakeBasicDamage(_inst.damagePerHit, self,
                    PvpDetector.AttackDir(_inst, target),
                    _inst.ConnectedUserAction, _inst.ImpactEffects);

                if (target.CanBePushed && _grabActorMethod != null) {
                    _grabActorMethod.Invoke(_inst, new object[] { target });
                }
            }
        }
    }
}
