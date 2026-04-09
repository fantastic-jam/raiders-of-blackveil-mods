using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    internal class PvpRhinoStampedeAbility {
        internal static readonly FieldInfo GrabbedEnemiesField = AccessTools.Field(typeof(RhinoStampedeAbility), "_grabbedEnemies");
        internal static readonly FieldInfo GrabbedThanThrowedField = AccessTools.Field(typeof(RhinoStampedeAbility), "_grabbedThanThrowedEnemies");
        internal static readonly MethodInfo GrabActorMethod = AccessTools.Method(typeof(RhinoStampedeAbility), "GrabActor", new[] { typeof(StatsManager) });

        private readonly RhinoStampedeAbility _inst;

        internal PvpRhinoStampedeAbility(RhinoStampedeAbility inst) { _inst = inst; }

        internal void DetectEnemies() {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.grabCollider == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(_inst.grabCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var grabbed = GrabbedEnemiesField?.GetValue(_inst) as List<StatsManager>;
            var throwed = GrabbedThanThrowedField?.GetValue(_inst) as List<StatsManager>;

            foreach (var target in hits) {
                if (!target.IsAlive) { continue; }
                if (grabbed != null && grabbed.Contains(target)) { continue; }
                if (throwed != null && throwed.Contains(target)) { continue; }

                target.TakeBasicDamage(_inst.damagePerHit, self,
                    PvpDetector.AttackDir(_inst, target),
                    _inst.ConnectedUserAction, _inst.ImpactEffects);

                if (target.CanBePushed && GrabActorMethod != null) {
                    GrabActorMethod.Invoke(_inst, new object[] { target });
                }
            }
        }
    }
}
