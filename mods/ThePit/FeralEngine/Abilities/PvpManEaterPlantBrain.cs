using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Input;
using RR.Game.Stats;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpManEaterPlantBrain {
        private static FieldInfo _closestTargetField;
        private static FieldInfo _closestTargetDistanceField;
        private static FieldInfo _aimAreaField;
        private static FieldInfo _dealDamageColliderField;
        private static FieldInfo _dealDamageAngleField;
        private static FieldInfo _damageField;

        internal static void Init() {
            _closestTargetField = AccessTools.Field(typeof(ManEaterPlantBrain), "_ClosestTarget");
            if (_closestTargetField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain._ClosestTarget not found — Man-Eater Plant won't target champions.");
            }

            _closestTargetDistanceField = AccessTools.Field(typeof(ManEaterPlantBrain), "_closestTargetDistance");
            if (_closestTargetDistanceField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain._closestTargetDistance not found — Man-Eater Plant targeting distance check inactive.");
            }

            _aimAreaField = AccessTools.Field(typeof(ManEaterPlantBrain), "aimArea");
            if (_aimAreaField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.aimArea not found — Man-Eater Plant target range uses default.");
            }

            _dealDamageColliderField = AccessTools.Field(typeof(ManEaterPlantBrain), "dealDamageCollider");
            if (_dealDamageColliderField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.dealDamageCollider not found — Man-Eater Plant PvP damage inactive.");
            }

            _dealDamageAngleField = AccessTools.Field(typeof(ManEaterPlantBrain), "dealDamageAngle");
            if (_dealDamageAngleField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.dealDamageAngle not found — Man-Eater Plant PvP damage arc inactive.");
            }

            _damageField = AccessTools.Field(typeof(ManEaterPlantBrain), "damage");
            if (_damageField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.damage not found — Man-Eater Plant PvP damage stats inactive.");
            }
        }

        private readonly ManEaterPlantBrain _inst;

        internal PvpManEaterPlantBrain(ManEaterPlantBrain inst) { _inst = inst; }

        internal void Aim() {
            if (!_inst.Object.HasStateAuthority) { return; }

            var creator = _inst.CreatorAbility?.Stats;
            float aimArea = _aimAreaField != null ? (float)_aimAreaField.GetValue(_inst) : 6f;
            float closestDist = aimArea;
            StatsManager closestStats = null;

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (creator != null && champ.Stats.ActorID == creator.ActorID) { continue; }
                if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

                float dist = Vector3.Distance(_inst.transform.position, champ.transform.position);
                if (dist < closestDist) { closestDist = dist; closestStats = champ.Stats; }
            }

            if (closestStats == null) { return; }

            // Set ClosestTarget backing field and distance so HasTarget computed property returns true.
            _closestTargetField?.SetValue(_inst, closestStats);
            _closestTargetDistanceField?.SetValue(_inst, closestDist - 1.5f);

            Vector3 dir = closestStats.transform.position - _inst.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) { return; }

            float maxRad = _inst.aimSpeed * Time.deltaTime;
            Vector3 forward = Vector3.RotateTowards(_inst.transform.forward, dir, maxRad, 0f);
            _inst.transform.rotation = Quaternion.LookRotation(forward);
        }

        internal void HitEnemiesInArch() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_dealDamageColliderField == null || _dealDamageAngleField == null || _damageField == null) { return; }

            var dealDamageCollider = _dealDamageColliderField.GetValue(_inst) as UnityEngine.CapsuleCollider;
            if (dealDamageCollider == null) { return; }

            var creator = _inst.CreatorAbility?.Stats;
            if (creator == null) { return; }

            var hits = PvpDetector.Overlap(dealDamageCollider, excludes: new[] { creator });
            if (hits.Count == 0) { return; }

            float dealDamageAngle = (float)_dealDamageAngleField.GetValue(_inst);
            float cosAngle = Mathf.Cos(dealDamageAngle * Mathf.Deg2Rad * 0.5f);
            var forward = _inst.transform.forward;
            var origin = _inst.transform.position;
            var dmg = (RR.Game.Stats.DamageDescriptor)_damageField.GetValue(_inst);

            foreach (var target in hits) {
                var dir = target.transform.position - origin;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f || Vector3.Dot(forward, dir.normalized) < cosAngle) { continue; }
                target.TakeBasicDamage(dmg, creator,
                    PvpDetector.AttackDir(_inst, target),
                    UserAction.Offensive, _inst.ImpactEffects);
            }
        }
    }
}
