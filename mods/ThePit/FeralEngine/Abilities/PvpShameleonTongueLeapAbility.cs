using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpShameleonTongueLeapAbility {
        private readonly ShameleonTongueLeapAbility _inst;
        private ChampionAbility.MainStateValues _prevMainState;
        private StatsManager _hitChampion;

        internal PvpShameleonTongueLeapAbility(ShameleonTongueLeapAbility inst) { _inst = inst; }

        internal void Prefix() {
            _prevMainState = _inst.MainState;

            // Expand tongueHitMask during WindUp so the original raycast can connect with champions.
            if (_inst.MainState == ChampionAbility.MainStateValues.Cast) {
                _inst.tongueHitMask = (LayerMask)(_inst.tongueHitMask.value | LayerMask.GetMask("Player"));
            }
        }

        internal void Postfix() {
            // Restore mask if prefix expanded it (runs on all clients for visual correctness).
            if (_inst.MainState == ChampionAbility.MainStateValues.Cast ||
                _prevMainState == ChampionAbility.MainStateValues.Cast) {
                _inst.tongueHitMask = (LayerMask)(_inst.tongueHitMask.value & ~LayerMask.GetMask("Player"));
            }

            if (_inst.Runner?.IsServer != true) { return; }

            var curState = _inst.MainState;
            var prevState = _prevMainState;

            // Cast → InAction: tongue flying — detect target.
            if (prevState == ChampionAbility.MainStateValues.Cast && curState == ChampionAbility.MainStateValues.InAction) {
                var self = _inst.Stats;
                var dir = _inst.transform.forward;
                dir.y = 0f;
                if (dir != Vector3.zero) { dir.Normalize(); }
                _hitChampion = PvpDetector.Raycast(
                    _inst.transform.position + _inst.TongueTipPos,
                    dir, _inst.tongueMaxDistance, excludes: new[] { self });
            }

            // InAction → Cancel: tongue came back without landing.
            if (prevState == ChampionAbility.MainStateValues.InAction && curState == ChampionAbility.MainStateValues.Cancel) {
                _hitChampion = null;
            }

            // InAction → FollowThrough: apply stun + damage.
            if (prevState == ChampionAbility.MainStateValues.InAction && curState == ChampionAbility.MainStateValues.FollowThrough) {
                var self = _inst.Stats;

                if (_hitChampion != null && _hitChampion.IsAlive && !_hitChampion.IsImmuneOrInvincible) {
                    _hitChampion.Movement?.GetStunned(_inst.hitStunDuration, self);
                    var dmg = _inst.hitDamage;
                    dmg.blessedAttack = self.IsBlessed;
                    dmg.furyAttack = self.HasFury;
                    _hitChampion.TakeBasicDamage(dmg, self,
                        PvpDetector.AttackDir(_inst, _hitChampion),
                        _inst.ConnectedUserAction, _inst.ImpactEffects);
                }

                var col = _inst.damageColliderAtLanding;
                if (col != null) {
                    var excludes = _hitChampion != null
                        ? new[] { self, _hitChampion }
                        : new[] { self };
                    foreach (var t in PvpDetector.Overlap(col, excludes: excludes)) {
                        var dmg = _inst.hitDamage;
                        dmg.blessedAttack = self.IsBlessed;
                        dmg.furyAttack = self.HasFury;
                        t.TakeBasicDamage(dmg, self,
                            PvpDetector.AttackDir(_inst, t),
                            _inst.ConnectedUserAction, _inst.ImpactEffects);
                    }
                }
                _hitChampion = null;
            }

            // Went idle — clean up.
            if (curState == ChampionAbility.MainStateValues.Idle) {
                _hitChampion = null;
            }
        }

        internal void Reset() {
            _hitChampion = null;
        }
    }
}
