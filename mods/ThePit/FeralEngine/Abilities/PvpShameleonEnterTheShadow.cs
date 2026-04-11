using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Perk;
using ThePit.FeralEngine;

namespace ThePit.FeralEngine.Abilities {
    // PvP sidecar for ShameleonEnterTheShadowAbility.
    // Replaces vanilla invisible with immune + 3× speed. AddInvisible is blocked
    // upstream so the stealth material never appears.
    // Each method here mirrors a patched public override on the ability class.
    internal class PvpShameleonEnterTheShadow {
        private const float SpeedBoostPct = 200f;
        private const int StateInactive = 0;
        private const int StateWindUp = 1;
        private const int StateActive = 2;

        private static PropertyInfo _innerStateProp;
        private static FieldInfo _pausedDueSapField;

        private readonly ShameleonEnterTheShadowAbility _inst;
        private int _prevInnerState;
        private bool _immuneApplied;
        private bool _speedApplied;

        internal static void Init() {
            _innerStateProp = AccessTools.Property(typeof(ChampionAbility), "InnerState");
            if (_innerStateProp == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbility.InnerState not found — Shameleon stealth buff inactive.");
            }

            _pausedDueSapField = AccessTools.Field(typeof(ChampionAbilityWithCooldown), "_cooldownTimerIsPausedDueSAP");
            if (_pausedDueSapField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown._cooldownTimerIsPausedDueSAP not found — stealth cooldown pause inactive.");
            }
        }

        internal PvpShameleonEnterTheShadow(ShameleonEnterTheShadowAbility inst) {
            _inst = inst;
            _prevInnerState = StateInactive;
        }

        // Forwarded from Spawned() — sidecar was just created fresh so nothing to do.
        internal void OnSpawned() { }

        // Forwarded from FixedUpdateNetwork().
        // Applies immune + speed on WindUp entry (the same tick vanilla calls AddInvisible,
        // now blocked). Treats WindUp and Active as "in shadow" so effects are live for
        // the full stealth window, not just from Active onward.
        internal void OnFixedUpdate() {
            if (!ThePitState.IsDraftMode) { return; }
            if (_innerStateProp == null) { return; }

            var stats = _inst.Stats;
            if (stats?.Health == null) { return; }

            int state = (int)_innerStateProp.GetValue(_inst);
            bool inShadow = state == StateWindUp || state == StateActive;
            bool wasInShadow = _prevInnerState == StateWindUp || _prevInnerState == StateActive;

            if (_inst.Object.HasStateAuthority) {
                if (inShadow && !wasInShadow) {
                    stats.Health.AddImmune();
                    stats.ModifyPropertyForFrames(Property.MovementSpeed, SpeedBoostPct, 999999);
                    _pausedDueSapField?.SetValue(_inst, true);
                    _immuneApplied = true;
                    _speedApplied = true;
                } else if (!inShadow && wasInShadow) {
                    if (_immuneApplied) {
                        stats.Health.RemoveImmune();
                        _immuneApplied = false;
                    }
                    if (_speedApplied) {
                        stats.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
                        _speedApplied = false;
                    }
                    _pausedDueSapField?.SetValue(_inst, false);
                }

                // Immune breaks the moment any non-defensive ability activates; speed continues.
                if (_immuneApplied && inShadow) {
                    var c = stats.Champion;
                    if (c != null &&
                        ((c.Attack != null && c.Attack.IsActive) ||
                         (c.Power != null && c.Power.IsActive) ||
                         (c.Special != null && c.Special.IsActive) ||
                         (c.Ultimate != null && c.Ultimate.IsActive))) {
                        stats.Health.RemoveImmune();
                        _immuneApplied = false;
                    }
                }
            }

            _prevInnerState = state;
        }

        // Forwarded from OnCharacterEvent().
        // Vanilla ends stealth when hit — the resulting InnerState transition in
        // OnFixedUpdate clears immune and speed. No extra handling needed here.
        internal void OnCharacterEvent() { }

        // Called on match end / FeralCore deactivation.
        internal void Reset() {
            if (_inst.Object.HasStateAuthority) {
                var stats = _inst.Stats;
                if (_immuneApplied) {
                    stats?.Health.RemoveImmune();
                    _immuneApplied = false;
                }
                if (_speedApplied) {
                    stats?.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
                    _pausedDueSapField?.SetValue(_inst, false);
                    _speedApplied = false;
                }
            }
            _immuneApplied = false;
            _speedApplied = false;
            _prevInnerState = _innerStateProp != null
                ? (int)_innerStateProp.GetValue(_inst)
                : StateInactive;
        }
    }
}
