using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Postfix on FixedUpdateNetwork. When the earthquake wave is active, we run the
    // same box-sweep that the original uses but against champions (Player layer).
    internal static class RhinoEarthquakePatch {
        private static MethodInfo _wavePosGetter;
        private static FieldInfo _waveStartPosField;
        private static FieldInfo _waveDirectionField;
        private static FieldInfo _hittedEnemiesField;

        internal static void Apply(Harmony harmony) {
            _wavePosGetter = AccessTools.PropertyGetter(typeof(RhinoEarthquakeAbility), "WaveLinearPosition");
            _waveStartPosField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_waveStartPos");
            _waveDirectionField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_waveDirection");
            _hittedEnemiesField = AccessTools.Field(typeof(RhinoEarthquakeAbility), "_hittedEnemies");

            if (_wavePosGetter == null || _waveStartPosField == null ||
                _waveDirectionField == null || _hittedEnemiesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility fields not found — Earthquake PvP inactive.");
            }

            var fun = AccessTools.Method(typeof(RhinoEarthquakeAbility), "FixedUpdateNetwork");
            if (fun == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility.FixedUpdateNetwork not found — Earthquake PvP inactive.");
                return;
            }
            harmony.Patch(fun,
                postfix: new HarmonyMethod(typeof(RhinoEarthquakePatch), nameof(FunPostfix)));
        }

        private static void FunPostfix(RhinoEarthquakeAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (_wavePosGetter == null || _waveStartPosField == null ||
                _waveDirectionField == null || _hittedEnemiesField == null) { return; }

            float wavePos = (float)_wavePosGetter.Invoke(__instance, null);
            if (wavePos <= 0f || wavePos > __instance.waveDistance) { return; }

            var startPos = (Vector3)_waveStartPosField.GetValue(__instance);
            var waveDir = (Vector3)_waveDirectionField.GetValue(__instance);
            var hitted = _hittedEnemiesField.GetValue(__instance) as List<StatsManager>;

            float width = Mathf.Lerp(__instance.waveStartWidth, __instance.waveFinishWidth,
                wavePos / __instance.waveDistance);
            var halfExtents = new Vector3(width * 0.5f, 0.75f, 0.25f);
            var orientation = Quaternion.LookRotation(waveDir, Vector3.up);
            var center = startPos + waveDir * wavePos;

            var self = __instance.Stats;
            var hits = PvpDetector.OverlapBox(center, halfExtents, orientation, excludes: new[] { self });

            foreach (var target in hits) {
                if (hitted != null && hitted.Contains(target)) { continue; }

                target.TakeBasicDamage(__instance.damagePerHit, self,
                    PvpDetector.AttackDir(__instance, target),
                    __instance.ConnectedUserAction, __instance.ImpactEffects);

                if (target.Movement != null) {
                    target.Movement.GetStunned(__instance.stunDuration, self);
                }

                hitted?.Add(target);
            }
        }
    }
}
