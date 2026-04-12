using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpRhinoEarthquakeAbility {
        private static MethodInfo _wavePosGetter;
        private static FieldInfo _waveStartPosField;
        private static FieldInfo _waveDirectionField;
        private static FieldInfo _hittedEnemiesField;

        private readonly RhinoEarthquakeAbility _inst;

        internal static void Init() {
            _wavePosGetter = AccessTools.PropertyGetter(typeof(RhinoEarthquakeAbility), "WaveLinearPosition");
            if (_wavePosGetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility.WaveLinearPosition not found — Earthquake wave position inactive.");
            }

            _waveStartPosField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_waveStartPos");
            if (_waveStartPosField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility._waveStartPos not found — Earthquake start position inactive.");
            }

            _waveDirectionField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_waveDirection");
            if (_waveDirectionField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility._waveDirection not found — Earthquake direction inactive.");
            }

            _hittedEnemiesField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_hittedEnemies");
            if (_hittedEnemiesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility._hittedEnemies not found — Earthquake hit deduplication inactive.");
            }
        }

        internal PvpRhinoEarthquakeAbility(RhinoEarthquakeAbility inst) { _inst = inst; }

        internal void OnFixedUpdate() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_wavePosGetter == null) { return; }
            if (_waveStartPosField == null) { return; }
            if (_waveDirectionField == null) { return; }

            float wavePos = (float)_wavePosGetter.Invoke(_inst, null);
            if (wavePos <= 0f || wavePos > _inst.waveDistance) { return; }

            var startPos = (Vector3)_waveStartPosField.GetValue(_inst);
            var waveDir = (Vector3)_waveDirectionField.GetValue(_inst);
            var hitted = _hittedEnemiesField?.GetValue(_inst) as List<StatsManager>;

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
