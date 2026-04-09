using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    internal class PvpShameleonAttackAbility {
        private readonly ShameleonAttackAbility _inst;

        internal PvpShameleonAttackAbility(ShameleonAttackAbility inst) { _inst = inst; }

        internal void DoHit() {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (_inst.Runner?.IsServer != true) { return; }

            bool isLast = PvpDetector.IsLastComboPhase(_inst);
            var col = isLast ? _inst.lastHitCollider : _inst.normalHitCollider;
            if (col == null) { return; }

            var self = _inst.Stats;
            var hits = PvpDetector.Overlap(col, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = isLast ? _inst.damageAtLastHit : _inst.damageAtNormalHit;
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            var fx = PvpDetector.GetCurrentPhaseFX(_inst);

            foreach (var target in hits) {
                target.TakeBasicDamage(dmg, self,
                    PvpDetector.AttackDir(_inst, target),
                    _inst.ConnectedUserAction, fx);
            }
            PvpDetector.ToggleHasHit(_inst);
        }
    }
}
