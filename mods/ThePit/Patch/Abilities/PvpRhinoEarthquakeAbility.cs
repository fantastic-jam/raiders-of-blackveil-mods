using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal class PvpRhinoEarthquakeAbility {
        internal static readonly MethodInfo WavePosGetter = AccessTools.PropertyGetter(typeof(RhinoEarthquakeAbility), "WaveLinearPosition");
        internal static readonly FieldInfo WaveStartPosField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_waveStartPos");
        internal static readonly FieldInfo WaveDirectionField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_waveDirection");
        internal static readonly FieldInfo HittedEnemiesField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_hittedEnemies");

        private readonly RhinoEarthquakeAbility _inst;

        internal PvpRhinoEarthquakeAbility(RhinoEarthquakeAbility inst) { _inst = inst; }

        internal void OnFixedUpdate() {
            if (!ThePitState.IsAttackPossible) { return; }
            if (_inst.Runner?.IsServer != true) { return; }
            if (WavePosGetter == null || WaveStartPosField == null ||
                WaveDirectionField == null || HittedEnemiesField == null) { return; }

            float wavePos = (float)WavePosGetter.Invoke(_inst, null);
            if (wavePos <= 0f || wavePos > _inst.waveDistance) { return; }

            var startPos = (Vector3)WaveStartPosField.GetValue(_inst);
            var waveDir = (Vector3)WaveDirectionField.GetValue(_inst);
            var hitted = HittedEnemiesField.GetValue(_inst) as List<StatsManager>;

            float width = Mathf.Lerp(_inst.waveStartWidth, _inst.waveFinishWidth,
                wavePos / _inst.waveDistance);
            var halfExtents = new Vector3(width * 0.5f, 0.75f, 0.25f);
            var orientation = Quaternion.LookRotation(waveDir, Vector3.up);
            var center = startPos + waveDir * wavePos;

            var self = _inst.Stats;
            var hits = PvpDetector.OverlapBox(center, halfExtents, orientation, excludes: new[] { self });

            foreach (var target in hits) {
                if (hitted != null && hitted.Contains(target)) { continue; }
                target.TakeBasicDamage(_inst.damagePerHit, self,
                    PvpDetector.AttackDir(_inst, target),
                    _inst.ConnectedUserAction, _inst.ImpactEffects);
                target.Movement?.GetStunned(_inst.stunDuration, self);
                hitted?.Add(target);
            }
        }
    }
}
