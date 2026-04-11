using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // Sidecar for RhinoAttackAbility that owns PvP-aware detectors and handles DoHit.
    // One instance is created per RhinoAttackAbility in Spawned() and stored via
    // RhinoAttackPatch._sidecars.
    internal class PvpRhinoAttackAbility {
        private readonly RhinoAttackAbility _inst;
        private readonly PvpActorColliderDetector _normalDetector;
        private readonly PvpActorColliderDetector _lastDetector;

        internal PvpRhinoAttackAbility(RhinoAttackAbility instance) {
            _inst = instance;
            var self = new[] { instance.Stats };
            _normalDetector = new PvpActorColliderDetector(
                new[] { instance.normalHitCollider1, instance.normalHitCollider2 }, self);
            _lastDetector = new PvpActorColliderDetector(instance.lastHitCollider1, self);
        }

        internal void DoHit() {
            if (_inst.Runner?.IsServer != true) { return; }

            bool isLast = PvpDetector.IsLastComboPhase(_inst);
            var hits = (isLast ? _lastDetector : _normalDetector).DoDetection();
            if (hits.Count == 0) { return; }

            var self = _inst.Stats;
            var dmg = isLast ? _inst.damageAtLastHit : _inst.damageAtNormalHit;
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            var fx = PvpDetector.GetCurrentPhaseFX(_inst);

            foreach (var target in hits) {
                var dir = PvpDetector.AttackDir(_inst, target);
                target.TakeBasicDamage(dmg, self, dir, _inst.ConnectedUserAction, fx);

                if (isLast && target.CanBePushed && target.Character != null) {
                    var pushDir = dir;
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude > 0.001f) {
                        // PvP: flat force, no distance falloff (unlike the PvE version which
                        // lerps 1→0.6 over 5 units). Intentional — distance-falloff feels
                        // unreliable in tight PvP engagements.
                        target.Character.AddPushForce(pushDir.normalized * _inst.pushForceLastHit);
                    }
                }
            }
            PvpDetector.ToggleHasHit(_inst);
        }
    }
}
