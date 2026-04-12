using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Stats;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Mirrors PvpManEaterPlantBrain: sets _hasTarget = true and rotates toward the nearest
    // enemy champion so the seed turret can transition to SeedState.Shoot.
    // Also returns true (fully aimed) when within 0.1 degrees, matching WitheredSeedBrain.Aim().
    internal class PvpWitheredSeedBrain {
        private static FieldInfo _hasTargetField;
        private static FieldInfo _reviverField;
        internal static FieldInfo CasterField { get; private set; }

        internal static void Init() {
            _hasTargetField = AccessTools.Field(typeof(WitheredSeedBrain), "_hasTarget");
            if (_hasTargetField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain._hasTarget not found — seed turret champion targeting inactive.");
            }

            _reviverField = AccessTools.Field(typeof(WitheredSeedBrain), "_reviver");
            if (_reviverField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain._reviver not found — seed turret champion targeting inactive.");
            }

            CasterField = AccessTools.Field(typeof(WitheredSeedBrain), "_projectileCaster");
            if (CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WitheredSeedBrain._projectileCaster not found — seed turret PvP projectiles inactive.");
            }
        }

        private readonly WitheredSeedBrain _inst;
        private bool _expanded;
        private ProjectileCaster _caster;
        private ProjectileCasterExpander.SavedMasks _saved;

        internal PvpWitheredSeedBrain(WitheredSeedBrain inst) {
            _inst = inst;
            TryExpand();
        }

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

            var reviver = _reviverField?.GetValue(_inst) as StatsManager;
            if (reviver == null) { return false; }

            float closestDist = float.PositiveInfinity;
            Transform closestChamp = null;

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (champ.Stats.ActorID == reviver.ActorID) { continue; }
                if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

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
            _hasTargetField?.SetValue(_inst, true);

            return Vector3.Angle(forward, dir) < 0.1f;
        }
    }
}
