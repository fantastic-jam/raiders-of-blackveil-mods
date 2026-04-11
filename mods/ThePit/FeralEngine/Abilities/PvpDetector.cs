using System.Collections.Generic;
using System.Reflection;
using Fusion;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using RR.Utility;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Universal replacement for all game detection (ActorColliderDetector, Physics.Overlap*, Physics.Raycast).
    // Every method accepts optional includes / excludes arrays to filter by StatsManager identity.
    //   includes — if non-null, only return entries in this list
    //   excludes — if non-null, skip entries in this list
    internal static class PvpDetector {
        private static readonly Collider[] _buffer = new Collider[32];

        private static int _pvpLayerMask = -1;

        internal static int PvpLayerMask {
            get {
                if (_pvpLayerMask < 0) {
                    _pvpLayerMask = ChampionAbility.ChampionDamageLayerMask | LayerMask.GetMask("Player");
                }
                return _pvpLayerMask;
            }
        }

        // ── Reflection handles (lazy) ────────────────────────────────────────────

        private static MethodInfo _hasHitGetter;
        private static MethodInfo _hasHitSetter;
        private static FieldInfo _attackPhasesField;
        private static MethodInfo _currentPhaseIndexGetter;
        private static FieldInfo _attackPhaseFxField;
        private static bool _reflectionReady;

        private static void EnsureReflection() {
            if (_reflectionReady) { return; }
            _reflectionReady = true;
            _hasHitGetter = AccessTools.PropertyGetter(typeof(ChampionAbility), "hasHit");
            _hasHitSetter = AccessTools.PropertySetter(typeof(ChampionAbility), "hasHit");
            _attackPhasesField = AccessTools.Field(typeof(ComboAttackAbility), "attackPhases");
            _currentPhaseIndexGetter = AccessTools.PropertyGetter(typeof(ComboAttackAbility), "CurrentPhaseIndex");
            if (_attackPhasesField != null) {
                // AttackPhase is a nested class — resolve FX field dynamically on first use
            }
        }

        // ── Detection ────────────────────────────────────────────────────────────

        // Dispatches to the correct typed overlap based on the collider's runtime type.
        internal static List<StatsManager> Overlap(
            Collider col,
            StatsManager[] includes = null,
            StatsManager[] excludes = null) {
            switch (col) {
                case BoxCollider box:
                    return OverlapBox(
                        box.transform.TransformPoint(box.center),
                        Vector3.Scale(box.size * 0.5f, AbsScale(box.transform.lossyScale)),
                        box.transform.rotation,
                        includes, excludes);
                case SphereCollider sphere:
                    float sr = sphere.radius * MaxAbsScale(sphere.transform.lossyScale);
                    return OverlapSphere(
                        sphere.transform.TransformPoint(sphere.center),
                        sr, includes, excludes);
                case CapsuleCollider cap:
                    return OverlapCapsuleFromCollider(cap, includes, excludes);
                default:
                    return new List<StatsManager>();
            }
        }

        internal static List<StatsManager> OverlapBox(
            Vector3 center, Vector3 halfExtents, Quaternion rotation,
            StatsManager[] includes = null,
            StatsManager[] excludes = null) {
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _buffer, rotation, PvpLayerMask);
            return FilterHits(_buffer, count, includes, excludes);
        }

        internal static List<StatsManager> OverlapSphere(
            Vector3 center, float radius,
            StatsManager[] includes = null,
            StatsManager[] excludes = null) {
            int count = Physics.OverlapSphereNonAlloc(center, radius, _buffer, PvpLayerMask);
            return FilterHits(_buffer, count, includes, excludes);
        }

        internal static List<StatsManager> OverlapCapsule(
            Vector3 p0, Vector3 p1, float radius,
            StatsManager[] includes = null,
            StatsManager[] excludes = null) {
            int count = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, _buffer, PvpLayerMask);
            return FilterHits(_buffer, count, includes, excludes);
        }

        internal static StatsManager Raycast(
            Vector3 origin, Vector3 direction, float maxDistance,
            StatsManager[] includes = null,
            StatsManager[] excludes = null) {
            var hits = Physics.RaycastAll(origin, direction, maxDistance, PvpLayerMask);
            float best = float.MaxValue;
            StatsManager result = null;
            foreach (var hit in hits) {
                var sm = hit.collider.GetComponentInParent<StatsManager>();
                if (sm == null) { continue; }
                if (!sm.IsChampion || !sm.IsAlive || sm.IsImmuneOrInvincible) { continue; }
                if (FeralCore.IsRespawnInvincible(sm.ActorID)) { continue; }
                if (includes != null && System.Array.IndexOf(includes, sm) < 0) { continue; }
                if (excludes != null && System.Array.IndexOf(excludes, sm) >= 0) { continue; }
                if (hit.distance < best) { best = hit.distance; result = sm; }
            }
            return result;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        // Toggle the protected NetworkBool hasHit to trigger hit-feedback render events.
        internal static void ToggleHasHit(ChampionAbility instance) {
            EnsureReflection();
            if (_hasHitGetter == null || _hasHitSetter == null) { return; }
            var cur = (NetworkBool)_hasHitGetter.Invoke(instance, null);
            _hasHitSetter.Invoke(instance, new object[] { (NetworkBool)(!(bool)cur) });
        }

        // Returns true if the current attack combo phase is the final phase.
        internal static bool IsLastComboPhase(ComboAttackAbility instance) {
            EnsureReflection();
            if (_attackPhasesField == null || _currentPhaseIndexGetter == null) { return false; }
            var phases = _attackPhasesField.GetValue(instance) as System.Collections.IList;
            if (phases == null || phases.Count == 0) { return false; }
            int idx = (int)_currentPhaseIndexGetter.Invoke(instance, null);
            return idx == phases.Count;
        }

        // Returns the FX of the current attack phase, or null if not resolvable.
        internal static EffectCollection GetCurrentPhaseFX(ComboAttackAbility instance) {
            EnsureReflection();
            if (_attackPhasesField == null || _currentPhaseIndexGetter == null) { return null; }
            var phases = _attackPhasesField.GetValue(instance) as System.Collections.IList;
            if (phases == null) { return null; }
            int idx = (int)_currentPhaseIndexGetter.Invoke(instance, null);
            if (idx <= 0 || idx > phases.Count) { return null; }
            var phase = phases[idx - 1];
            if (phase == null) { return null; }
            if (_attackPhaseFxField == null) {
                _attackPhaseFxField = AccessTools.Field(phase.GetType(), "FX");
            }
            return _attackPhaseFxField?.GetValue(phase) as EffectCollection;
        }

        // World-space direction from attacker to target.
        internal static Vector3 AttackDir(Component attacker, StatsManager target) {
            return target.transform.position - attacker.transform.position;
        }

        // Returns true when a and b should be treated as enemies.
        // FFA (no teams assigned): always true.
        // Team-based: same team = allies (false), different teams = enemies (true), unassigned = enemy of all (true).
        internal static bool AreEnemies(StatsManager a, StatsManager b) {
            var teamA = FeralCore.GetTeam(a);
            var teamB = FeralCore.GetTeam(b);
            if (teamA == null || teamB == null) { return true; }
            return teamA.Value != teamB.Value;
        }

        // ── Private ──────────────────────────────────────────────────────────────

        private static List<StatsManager> OverlapCapsuleFromCollider(
            CapsuleCollider cap,
            StatsManager[] includes,
            StatsManager[] excludes) {
            Vector3 center = cap.transform.TransformPoint(cap.center);
            Vector3 axisDir;
            float heightScale;
            switch (cap.direction) {
                case 0:
                    axisDir = cap.transform.right;
                    heightScale = Mathf.Abs(cap.transform.lossyScale.x);
                    break;
                case 2:
                    axisDir = cap.transform.forward;
                    heightScale = Mathf.Abs(cap.transform.lossyScale.z);
                    break;
                default: // 1 = Y
                    axisDir = cap.transform.up;
                    heightScale = Mathf.Abs(cap.transform.lossyScale.y);
                    break;
            }
            float halfH = Mathf.Max(0f, cap.height * 0.5f - cap.radius) * heightScale;
            float r = cap.radius * MaxAbsScale(cap.transform.lossyScale);
            return OverlapCapsule(center - axisDir * halfH, center + axisDir * halfH, r, includes, excludes);
        }

        private static List<StatsManager> FilterHits(
            Collider[] buffer, int count,
            StatsManager[] includes,
            StatsManager[] excludes) {
            var result = new List<StatsManager>(4);
            for (int i = 0; i < count; i++) {
                var col = buffer[i];
                if (col == null) { continue; }
                // TryGetComponent only checks the exact GameObject; champion StatsManager is on the
                // root while the Player-layer collider may be on a child — use GetComponentInParent.
                var sm = col.GetComponentInParent<StatsManager>();
                if (sm == null) { continue; }
                if (!sm.IsChampion || !sm.IsAlive || sm.IsImmuneOrInvincible) { continue; }
                if (FeralCore.IsRespawnInvincible(sm.ActorID)) { continue; }
                if (includes != null && System.Array.IndexOf(includes, sm) < 0) { continue; }
                if (excludes != null && System.Array.IndexOf(excludes, sm) >= 0) { continue; }
                if (!result.Contains(sm)) { result.Add(sm); }
            }
            return result;
        }

        private static Vector3 AbsScale(Vector3 s) =>
            new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

        private static float MaxAbsScale(Vector3 s) =>
            Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }
}
