using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Mirrors PvpManEaterPlantBrain: sets _hasTarget = true and rotates toward the nearest
    // enemy champion so the seed turret can transition to SeedState.Shoot.
    // Also returns true (fully aimed) when within 0.1 degrees, matching WitheredSeedBrain.Aim().
    internal class PvpWitheredSeedBrain {
        internal static readonly FieldInfo HasTargetField = AccessTools.Field(typeof(WitheredSeedBrain), "_hasTarget");
        internal static readonly FieldInfo ReviverField = AccessTools.Field(typeof(WitheredSeedBrain), "_reviver");
        internal static readonly FieldInfo CasterField = AccessTools.Field(typeof(WitheredSeedBrain), "_projectileCaster");

        private readonly WitheredSeedBrain _inst;
        private bool _expanded;
        private ProjectileCaster _caster;
        private ProjectileCasterExpander.SavedMasks _saved;

        internal PvpWitheredSeedBrain(WitheredSeedBrain inst) { _inst = inst; }

        internal void TryExpand() {
            if (ThePitState.IsDraftMode && ThePitState.ArenaEntered) { Expand(); }
        }

        internal void Expand() {
            if (_expanded || !ProjectileCasterExpander.IsReady) { return; }
            _caster = CasterField?.GetValue(_inst) as ProjectileCaster;
            if (_caster != null) { _saved = ProjectileCasterExpander.Expand(_caster); }
            _expanded = true;
        }

        internal void Reset() {
            if (!_expanded) { return; }
            ProjectileCasterExpander.Reset(_caster, _saved);
            _expanded = false;
        }

        // Returns true when fully aimed at a champion (mirrors the original's angle < 0.1f check).
        internal bool Aim() {
            if (!_inst.Object.HasStateAuthority) { return false; }

            var reviver = ReviverField?.GetValue(_inst) as StatsManager;
            if (reviver == null) { return false; }

            float closestDist = float.PositiveInfinity;
            Transform closestChamp = null;

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (champ.Stats.ActorID == reviver.ActorID) { continue; }
                if (ThePitState.IsPlayerInvincible(champ.Stats.ActorID)) { continue; }

                float dist = Vector3.Distance(_inst.transform.position, champ.transform.position);
                if (dist < closestDist) { closestDist = dist; closestChamp = champ.transform; }
            }

            if (closestChamp == null) { return false; }

            Vector3 dir = closestChamp.position - _inst.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) { return false; }

            float maxRad = _inst.aimSpeed * Time.deltaTime;
            Vector3 forward = Vector3.RotateTowards(_inst.transform.forward, dir, maxRad, 0f);
            _inst.transform.rotation = Quaternion.LookRotation(forward);
            HasTargetField?.SetValue(_inst, true);

            return Vector3.Angle(forward, dir) < 0.1f;
        }
    }
}
