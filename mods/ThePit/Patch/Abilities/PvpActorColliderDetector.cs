using System.Collections.Generic;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // PvP-aware replacement for ActorColliderDetector.
    // Always uses PvpDetector.PvpLayerMask (which includes the Player layer so
    // champions can hit each other). Accepts an exclusion list to skip the caster.
    //
    // No Target parameter — the mask is intentionally fixed to PvpLayerMask for all
    // PvP detectors. If you need a different mask, extend PvpDetector directly.
    internal class PvpActorColliderDetector {
        private readonly List<BoxCollider> _box = new();
        private readonly List<SphereCollider> _sphere = new();
        private readonly List<CapsuleCollider> _capsule = new();
        private readonly StatsManager[] _excludes;

        internal PvpActorColliderDetector(BoxCollider[] colliders, StatsManager[] excludes) {
            _box.AddRange(colliders);
            _excludes = excludes;
        }

        internal PvpActorColliderDetector(BoxCollider collider, StatsManager[] excludes) {
            _box.Add(collider);
            _excludes = excludes;
        }

        internal PvpActorColliderDetector(SphereCollider collider, StatsManager[] excludes) {
            _sphere.Add(collider);
            _excludes = excludes;
        }

        internal PvpActorColliderDetector(CapsuleCollider collider, StatsManager[] excludes) {
            _capsule.Add(collider);
            _excludes = excludes;
        }

        internal List<StatsManager> DoDetection() {
            var result = new List<StatsManager>(4);
            foreach (var c in _box) {
                if (c == null) { continue; }
                foreach (var sm in PvpDetector.Overlap(c, excludes: _excludes)) {
                    if (!result.Contains(sm)) { result.Add(sm); }
                }
            }
            foreach (var c in _sphere) {
                if (c == null) { continue; }
                foreach (var sm in PvpDetector.Overlap(c, excludes: _excludes)) {
                    if (!result.Contains(sm)) { result.Add(sm); }
                }
            }
            foreach (var c in _capsule) {
                if (c == null) { continue; }
                foreach (var sm in PvpDetector.Overlap(c, excludes: _excludes)) {
                    if (!result.Contains(sm)) { result.Add(sm); }
                }
            }
            return result;
        }
    }
}
