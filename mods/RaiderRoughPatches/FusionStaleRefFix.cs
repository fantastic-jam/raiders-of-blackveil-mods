using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace RaiderRoughPatches {
    // Clears stale Fusion NetworkBehaviourId backing fields that cause per-tick
    // "Failed to unwrap" spam when a NetworkObject is despawned before another
    // object that holds a [Networked] ref to it.
    //
    //   SunStrikeArea.Caster      — caster champion despawned before SunStrikeArea at run end
    //   NetworkCharacterBase.PullCenter — pull-source despawned before grabbed target
    //
    // Unity "fake-null": when the backing field's GameObject is destroyed, the C# managed ref
    // survives but Unity operator== returns null. CopyBackingFieldsToState calls NetworkWrap
    // on the fake-null ref and re-encodes the stale ObjectId every tick — clients spam
    // "Failed to unwrap". Detected via !ReferenceEquals + (Object)== null, no NetworkUnwrap call.
    internal static class FusionStaleRefFix {
        private static FieldInfo _casterBackingField;
        private static FieldInfo _pullCenterBackingField;
        private static MethodInfo _pullCenterSetter;

        internal static void Init() {
            _casterBackingField = AccessTools.Field(typeof(SunStrikeArea), "_Caster");
            if (_casterBackingField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: SunStrikeArea._Caster not found — SunStrikeArea stale ref guard inactive.");
            }

            _pullCenterBackingField = AccessTools.Field(typeof(NetworkCharacterBase), "_PullCenter");
            if (_pullCenterBackingField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: NetworkCharacterBase._PullCenter not found — PullCenter stale ref guard inactive.");
            }

            _pullCenterSetter = AccessTools.PropertySetter(typeof(NetworkCharacterBase), "PullCenter");
            if (_pullCenterSetter == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: NetworkCharacterBase.PullCenter setter not found — PullCenter stale ref guard inactive.");
            }
        }

        // Returns false to skip FixedUpdateNetwork when Caster is fake-null — the area is
        // immediately despawned so re-encoding the stale id would just extend the spam window.
        internal static bool OnSunStrikeFixedUpdate(SunStrikeArea instance) {
            if (!instance.HasStateAuthority) { return true; }
            if (_casterBackingField == null) { return true; }

            var cached = _casterBackingField.GetValue(instance) as NetworkCharacterBase;
            if (ReferenceEquals(cached, null)) { return true; }
            if ((Object)cached != null) { return true; }

            _casterBackingField.SetValue(instance, null);
            instance.Caster = null;
            instance.Runner.Despawn(instance.Object);
            return false;
        }

        internal static void OnCharBaseFixedUpdate(NetworkCharacterBase instance) {
            if (!instance.HasStateAuthority) { return; }
            if (_pullCenterBackingField == null || _pullCenterSetter == null) { return; }

            var cached = _pullCenterBackingField.GetValue(instance) as NetworkCharacterBase;
            if (ReferenceEquals(cached, null)) { return; }
            if ((Object)cached != null) { return; }

            _pullCenterBackingField.SetValue(instance, null);
            _pullCenterSetter.Invoke(instance, new object[] { null });
        }
    }
}
