using RR.Game.Character;
using RR.Game.Input;

namespace ThePit.Patch.Abilities {
    internal class PvpSunStrikeArea {
        private readonly SunStrikeArea _inst;
        private bool _fired;

        internal PvpSunStrikeArea(SunStrikeArea inst) { _inst = inst; }

        internal void DamageCheck() {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (_inst.Runner?.IsServer != true) { return; }
            if (_fired) { return; }
            _fired = true;

            var caster = _inst.Caster;
            if (caster == null || _inst.HitCollider == null) { return; }
            var self = caster.Stats;
            if (self == null) { return; }

            var hits = PvpDetector.Overlap(_inst.HitCollider, excludes: new[] { self });
            foreach (var target in hits) {
                target.TakeBasicDamage(_inst.Damage, self,
                    PvpDetector.AttackDir(_inst, target), UserAction.Offensive);
            }
        }

        internal void Reset() { _fired = false; }
    }
}
