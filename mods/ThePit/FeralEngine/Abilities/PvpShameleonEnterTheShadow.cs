using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Perk;
using ThePit.FeralEngine;

namespace ThePit.FeralEngine.Abilities {
    // Manages immunity, triple move speed, and cooldown pause while Shameleon is in stealth in PvP.
    // Uses InnerState == Active (2) instead of IsInvisible to avoid false-triggering during the
    // arena grace period (which also calls AddInvisible on all champions).
    // Immune + cooldown pause: HasStateAuthority (networked state, runs on host for all objects).
    // Speed boost: HasInputAuthority (each client applies locally for their own champion).
    internal class PvpShameleonEnterTheShadow {
        private const float SpeedBoostPct = 200f; // +200% on top of base = 3× total
        private const int InnerStateActive = 2;   // ShameleonEnterTheShadowAbility.InternalState.Active

        private static PropertyInfo _innerStateProp;
        private static FieldInfo _pausedDueSapField;

        private readonly ShameleonEnterTheShadowAbility _inst;
        private bool _wasInShadow;

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

            if (isInShadow != _wasInShadow) {
                if (_inst.Object.HasStateAuthority) {
                    if (isInShadow) {
                        stats.Health.AddImmune();
                        _pausedDueSapField?.SetValue(_inst, true);
                    } else {
                        stats.Health.RemoveImmune();
                        _pausedDueSapField?.SetValue(_inst, false);
                    }
                }
                if (_inst.Object.HasInputAuthority) {
                    if (isInShadow) { stats.ModifyPropertyForFrames(Property.MovementSpeed, SpeedBoostPct, 999999); } else { stats.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct); }
                }
            }

            _wasInShadow = isInShadow;
        }

        // Called on match end / FeralCore deactivation to clean up any lingering modifiers.
        internal void Reset() {
            if (!_wasInShadow) { return; }
            var stats = _inst.Stats;
            if (_inst.Object.HasStateAuthority) {
                stats?.Health.RemoveImmune();
                _pausedDueSapField?.SetValue(_inst, false);
            }
            if (_inst.Object.HasInputAuthority) {
                stats?.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct);
            }
            _wasInShadow = false;
        }
    }
}
