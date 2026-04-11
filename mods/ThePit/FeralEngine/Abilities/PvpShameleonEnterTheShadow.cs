using RR.Game.Character;
using RR.Game.Perk;
using ThePit.FeralEngine;

namespace ThePit.FeralEngine.Abilities {
    // Manages immunity and triple move speed while Shameleon is in stealth in PvP.
    // Tracks IsInvisible transitions to pair AddImmune/RemoveImmune and the speed modifier.
    internal class PvpShameleonEnterTheShadow {
        private const float SpeedBoostPct = 200f; // +200% on top of base = 3× total

        private readonly ShameleonEnterTheShadowAbility _inst;
        private bool _wasInShadow;

        internal PvpShameleonEnterTheShadow(ShameleonEnterTheShadowAbility inst) {
            _inst = inst;
        }

        internal void OnFixedUpdate() {
            if (!_inst.Object.HasStateAuthority || !ThePitState.IsDraftMode) { return; }

            var stats = _inst.Stats;
            if (stats?.Health == null) { return; }

            bool isInShadow = stats.Health.IsInvisible;

            if (isInShadow && !_wasInShadow) {
                stats.Health.AddImmune();
                stats.ModifyPropertyForFrames(Property.MovementSpeed, SpeedBoostPct, 999999);
            } else if (!isInShadow && _wasInShadow) {
                stats.Health.RemoveImmune();
                stats.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
            }

            _wasInShadow = isInShadow;
        }

        // Called on match end / FeralCore deactivation to clean up any lingering modifiers.
        internal void Reset() {
            if (!_wasInShadow) { return; }
            var stats = _inst.Stats;
            stats?.Health.RemoveImmune();
            stats?.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
            _wasInShadow = false;
        }
    }
}
