using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpShameleonShadowStrikeAbility {
        private readonly ShameleonShadowStrikeAbility _inst;

        internal PvpShameleonShadowStrikeAbility(ShameleonShadowStrikeAbility inst) { _inst = inst; }

        internal void DoHit() {
            if (_inst.Runner?.IsServer != true) { return; }

            var self = _inst.Stats;
            var excludes = new[] { self };
            bool anyHit = false;

            if (_inst.hitCollider1 != null) {
                var col = _inst.hitCollider1;
                var hits = PvpDetector.OverlapBox(
                    col.transform.TransformPoint(col.center),
                    Vector3.Scale(col.size * 0.5f, AbsScale(col.transform.lossyScale)),
                    col.transform.rotation, excludes: excludes);
                var dmg = _inst.damage;
                dmg.blessedAttack = self.IsBlessed;
                dmg.furyAttack = self.HasFury;
                foreach (var target in hits) {
                    target.TakeBasicDamage(dmg, self,
                        PvpDetector.AttackDir(_inst, target),
                        _inst.ConnectedUserAction, _inst.ImpactEffects);
                    anyHit = true;
                }
            }

            if (_inst.hitCollider2 != null) {
                var col = _inst.hitCollider2;
                var hits = PvpDetector.OverlapBox(
                    col.transform.TransformPoint(col.center),
                    Vector3.Scale(col.size * 0.5f, AbsScale(col.transform.lossyScale)),
                    col.transform.rotation, excludes: excludes);
                var dmg = _inst.damage;
                dmg.blessedAttack = self.IsBlessed;
                dmg.furyAttack = self.HasFury;
                foreach (var target in hits) {
                    target.TakeBasicDamage(dmg, self,
                        PvpDetector.AttackDir(_inst, target),
                        _inst.ConnectedUserAction, _inst.ImpactEffects);
                    anyHit = true;
                }
            }

            if (anyHit) { PvpDetector.ToggleHasHit(_inst); }
        }

        private static Vector3 AbsScale(Vector3 s) =>
            new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }
}
