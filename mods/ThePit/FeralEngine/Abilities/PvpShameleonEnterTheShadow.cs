using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Perk;
using ThePit.FeralEngine;

namespace ThePit.FeralEngine.Abilities {
    // Manages immunity, triple move speed, and cooldown pause while Shameleon is in stealth in PvP.
    // Immune lasts until Shameleon activates any non-defensive ability (attack, power, special,
    // ultimate) or stealth ends naturally — after that Shameleon is hittable but speed continues.
    // Speed boost and cooldown pause are tied to InnerState == Active (stealth active).
    // All state changes run on HasStateAuthority (host).
    // Speed uses PropertyModifierTimeouts ([Networked]) so it replicates to all clients.
    internal class PvpShameleonEnterTheShadow {
        private const float SpeedBoostPct = 200f; // +200% on top of base = 3× total
        private const int InnerStateActive = 2;   // ShameleonEnterTheShadowAbility.InternalState.Active

        private static PropertyInfo _innerStateProp;
        private static FieldInfo _pausedDueSapField;

        private readonly ShameleonEnterTheShadowAbility _inst;
        private bool _wasInShadow;
        private bool _immuneApplied;

        internal static void Init() {
            _innerStateProp = AccessTools.Property(typeof(ChampionAbility), "InnerState");
            if (_innerStateProp == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbility.InnerState not found — stealth state detection inactive.");
            }
            _pausedDueSapField = AccessTools.Field(typeof(ChampionAbilityWithCooldown), "_cooldownTimerIsPausedDueSAP");
            if (_pausedDueSapField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown._cooldownTimerIsPausedDueSAP not found — stealth cooldown pause inactive.");
            }
        }

        internal PvpShameleonEnterTheShadow(ShameleonEnterTheShadowAbility inst) {
            _inst = inst;
        }

        internal void OnFixedUpdate() {
            if (!ThePitState.IsDraftMode) { return; }
            if (_innerStateProp == null) { return; }

            var stats = _inst.Stats;
            if (stats?.Health == null) { return; }

            bool isInShadow = (int)_innerStateProp.GetValue(_inst) == InnerStateActive;

            if (_inst.Object.HasStateAuthority) {
                if (isInShadow && !_wasInShadow) {
                    // Stealth entered: grant all modifiers.
                    stats.Health.AddImmune();
                    stats.ModifyPropertyForFrames(Property.MovementSpeed, SpeedBoostPct, 999999);
                    _pausedDueSapField?.SetValue(_inst, true);
                    _immuneApplied = true;
                } else if (!isInShadow && _wasInShadow) {
                    // Stealth ended: clear all modifiers.
                    if (_immuneApplied) {
                        stats.Health.RemoveImmune();
                        _immuneApplied = false;
                    }
                    stats.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
                    _pausedDueSapField?.SetValue(_inst, false);
                }

                // Immune breaks the moment any non-defensive ability activates (stealth visual/speed continue).
                // Defensive is excluded — that slot IS Enter the Shadow and is always active during stealth.
                if (_immuneApplied && isInShadow) {
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

            _wasInShadow = isInShadow;
        }

        // Called on match end / FeralCore deactivation to clean up any lingering modifiers.
        // Sets _wasInShadow to the current actual state rather than false so that if one more
        // FixedUpdateNetwork tick fires before patches are removed, no false re-entry is detected.
        internal void Reset() {
            var stats = _inst.Stats;
            if (_inst.Object.HasStateAuthority) {
                if (_immuneApplied) {
                    stats?.Health.RemoveImmune();
                    _immuneApplied = false;
                }
                if (_wasInShadow) {
                    stats?.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
                    _pausedDueSapField?.SetValue(_inst, false);
                }
            }
            _immuneApplied = false;
            _wasInShadow = _innerStateProp != null && (int)_innerStateProp.GetValue(_inst) == InnerStateActive;
        }
    }
}
