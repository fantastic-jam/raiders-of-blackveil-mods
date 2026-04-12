using System.Reflection;
using Fusion;
using HarmonyLib;
using RR.Game;
using RR.Game.Character;
using RR.Game.Input;
using RR.Game.Perk;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Full PvP implementation of ShameleonEnterTheShadowAbility (proxy pattern).
    // Vanilla FixedUpdateNetwork and OnCharacterEvent are blocked by proxy prefixes in
    // ShameleonEnterTheShadowPatch; this class owns all ability logic.
    // Replicates the vanilla state machine with immune + 3× speed instead of invisible.
    internal class PvpShameleonEnterTheShadow {
        private const float SpeedBoostPct = 200f;

        // Ordinals mirror ShameleonEnterTheShadowAbility.InternalState (private enum)
        private const int Inactive = 0;
        private const int WindUp = 1;
        private const int Active = 2;
        private const int FollowThrough = 3;
        private const int CancelFrames = 4;

        private static FieldInfo _stealthDurationField;
        private static FieldInfo _windUpFramesField;
        private static FieldInfo _attackFramesField;
        private static FieldInfo _followThroughFramesField;
        private static FieldInfo _cancelFramesField;
        private static PropertyInfo _stealthTimerProp;
        private static PropertyInfo _innerStateProp;
        private static MethodInfo _startInnerStateMethod;
        private static PropertyInfo _currentStateRemainingFramesProp;
        private static FieldInfo _inputField;
        private static MethodInfo _windupValueForUISetter;

        private readonly ShameleonEnterTheShadowAbility _inst;
        private bool _immuneApplied;
        private bool _speedApplied;

        internal static void Init() {
            var absType = typeof(ShameleonEnterTheShadowAbility);
            var baseType = typeof(ChampionAbility);

            _stealthDurationField = AccessTools.Field(absType, "_stealthDuration");
            _windUpFramesField = AccessTools.Field(absType, "_windUpFrames");
            _attackFramesField = AccessTools.Field(absType, "_attackFrames");
            _followThroughFramesField = AccessTools.Field(absType, "_followThroughFrames");
            _cancelFramesField = AccessTools.Field(absType, "_cancelFrames");
            _stealthTimerProp = AccessTools.Property(absType, "StealthTimer");
            _innerStateProp = AccessTools.Property(baseType, "InnerState");
            _startInnerStateMethod = AccessTools.Method(baseType, "StartInnerState", new[] { typeof(int), typeof(int) });
            _currentStateRemainingFramesProp = AccessTools.Property(baseType, "CurrentStateRemainingFrames");
            _inputField = AccessTools.Field(baseType, "_input");
            _windupValueForUISetter = AccessTools.PropertySetter(baseType, "WindupValueForUI");

            if (_stealthDurationField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: _stealthDuration not found.");
            }

            if (_windUpFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: _windUpFrames not found.");
            }

            if (_attackFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: _attackFrames not found.");
            }

            if (_followThroughFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: _followThroughFrames not found.");
            }

            if (_cancelFramesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: _cancelFrames not found.");
            }

            if (_stealthTimerProp == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: StealthTimer not found.");
            }

            if (_innerStateProp == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: InnerState not found — Shameleon state machine inactive.");
            }

            if (_startInnerStateMethod == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: StartInnerState not found.");
            }

            if (_currentStateRemainingFramesProp == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: CurrentStateRemainingFrames not found.");
            }

            if (_inputField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: _input not found.");
            }

            if (_windupValueForUISetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: WindupValueForUI setter not found.");
            }
        }

        internal PvpShameleonEnterTheShadow(ShameleonEnterTheShadowAbility inst) {
            _inst = inst;
        }

        private float GetFloat(FieldInfo f, float fallback = 0f) =>
            f != null ? (float)f.GetValue(_inst) : fallback;

        private TickTimer StealthTimer {
            get => _stealthTimerProp != null ? (TickTimer)_stealthTimerProp.GetValue(_inst) : TickTimer.None;
            set => _stealthTimerProp?.SetValue(_inst, value);
        }

        private int InnerState =>
            _innerStateProp != null ? (int)_innerStateProp.GetValue(_inst) : Inactive;

        private int RemainingFrames =>
            _currentStateRemainingFramesProp != null ? (int)_currentStateRemainingFramesProp.GetValue(_inst) : 0;

        private void SetWindupValue(float? value) => _windupValueForUISetter?.Invoke(_inst, new object[] { value });

        private void StartState(int state, float frames = 0f) =>
            _startInnerStateMethod?.Invoke(_inst, new object[] { state, Mathf.RoundToInt(frames) });

        private bool IsButtonDown() {
            if (_inputField == null) { return false; }
            var input = _inputField.GetValue(_inst) as NetworkCharacterInputController;
            return input != null && input.IsButtonDown(_inst.ConnectedUserAction);
        }

        private void ApplyStealth() {
            var stats = _inst.Stats;
            if (!_immuneApplied) { stats.Health.AddImmune(); _immuneApplied = true; }
            if (!_speedApplied) { stats.ModifyPropertyForFrames(Property.MovementSpeed, SpeedBoostPct, 999999); _speedApplied = true; }
        }

        private void RemoveStealth() {
            var stats = _inst.Stats;
            if (_immuneApplied) { stats?.Health.RemoveImmune(); _immuneApplied = false; }
            if (_speedApplied) { stats?.ClearTemporaryModifiedProperty(Property.MovementSpeed, SpeedBoostPct); _speedApplied = false; }
        }

        internal void OnFixedUpdate() {
            if (!_inst.Object.HasStateAuthority) { return; }
            var stats = _inst.Stats;
            if (stats?.Health == null) { return; }

            float stealthDuration = GetFloat(_stealthDurationField, 4f);
            float windUpFrames = GetFloat(_windUpFramesField);
            float attackFrames = GetFloat(_attackFramesField);
            float followThruFrames = GetFloat(_followThroughFramesField);
            float cancelFrames = GetFloat(_cancelFramesField);

            SetWindupValue(null);

            // Independent stealth timer — removes effects if duration expires before state exits
            if (StealthTimer.Expired(_inst.Runner)) {
                RemoveStealth();
                StealthTimer = TickTimer.None;
            }

            switch (InnerState) {
                case Inactive:
                    if (!IsButtonDown() || !_inst.CanActivate) { break; }
                    StartState(WindUp, windUpFrames);
                    if (stats.PerkHandler != null && _inst.perksToActivate != null) {
                        foreach (var perk in _inst.perksToActivate) {
                            if (perk != null) { stats.PerkHandler.CollectPerkOnHost(perk); }
                        }
                    }
                    if (!StealthTimer.IsRunning) { ApplyStealth(); }
                    StealthTimer = TickTimer.CreateFromSeconds(_inst.Runner,
                        stealthDuration * _inst.ActualDurationMultiplier);
                    break;
                case WindUp:
                    if (windUpFrames > 0f) {
                        SetWindupValue(1f - (float)RemainingFrames / windUpFrames);
                    }
                    if (_inst.StateTimerExpired) { StartState(Active, attackFrames); }
                    break;
                case Active:
                    if (_inst.StateTimerExpired) { StartState(FollowThrough, followThruFrames - cancelFrames); }
                    break;
                case FollowThrough:
                    if (_inst.StateTimerExpired) {
                        StartState(cancelFrames > 0f ? CancelFrames : Inactive, cancelFrames);
                    }
                    break;
                case CancelFrames:
                    if (_inst.StateTimerExpired) { StartState(Inactive); }
                    break;
            }
        }

        internal void OnCharacterEvent(CharacterEvent gameplayEvent) {
            if (!_inst.Object.HasStateAuthority) { return; }
            if (StealthTimer.IsRunning
                && (gameplayEvent == CharacterEvent.OnHitWithAttack || gameplayEvent == CharacterEvent.OnHitWithPower)) {
                RemoveStealth();
                StealthTimer = TickTimer.None;
            }
        }

        internal void Reset() {
            RemoveStealth();
        }
    }
}
