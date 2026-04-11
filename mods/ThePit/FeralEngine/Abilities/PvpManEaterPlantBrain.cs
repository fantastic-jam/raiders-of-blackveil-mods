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
        internal static readonly FieldInfo HasTargetField = AccessTools.Field(typeof(ManEaterPlantBrain), "hasTarget");

        private readonly ManEaterPlantBrain _inst;

        internal PvpManEaterPlantBrain(ManEaterPlantBrain inst) { _inst = inst; }

        internal void Aim() {
            if (!_inst.Object.HasStateAuthority) { return; }

            var creator = _inst._creator;
            float closestDist = _inst.aimArea;
            Transform closestChamp = null;

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (creator != null && champ.Stats.ActorID == creator.ActorID) { continue; }
                if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

                float dist = Vector3.Distance(_inst.transform.position, champ.transform.position);
                if (dist < closestDist) { closestDist = dist; closestChamp = champ.transform; }
            }

            if (closestChamp == null) { return; }

            Vector3 dir = closestChamp.position - _inst.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) { return; }

            float maxRad = _inst.aimSpeed * Time.deltaTime;
            Vector3 forward = Vector3.RotateTowards(_inst.transform.forward, dir, maxRad, 0f);
            _inst.transform.rotation = Quaternion.LookRotation(forward);
            HasTargetField?.SetValue(_inst, true);
        }

        internal void HitEnemiesInArch() {
            if (_inst.Runner?.IsServer != true) { return; }
            if (_inst.dealDamageCollider == null) { return; }

            var creator = _inst._creator;
            if (creator == null) { return; }

            var hits = PvpDetector.Overlap(_inst.dealDamageCollider, excludes: new[] { creator });
            if (hits.Count == 0) { return; }

            float cosAngle = Mathf.Cos(_inst.dealDamageAngle * Mathf.Deg2Rad * 0.5f);
            var forward = _inst.transform.forward;
            var origin = _inst.transform.position;
            var dmg = _inst.damage;

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
