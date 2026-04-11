using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // In PvP: Enter the Shadow no longer breaks on hit — instead grants full invulnerability
    // and triple movement speed for the stealth duration.
    internal static class ShameleonEnterTheShadowPatch {
        private static readonly ConditionalWeakTable<ShameleonEnterTheShadowAbility, PvpShameleonEnterTheShadow> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var fixedUpdate = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(FixedUpdateNetworkPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.FixedUpdateNetwork not found — stealth buff inactive.");
            }

            var onCharEvent = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "OnCharacterEvent");
            if (onCharEvent != null) {
                harmony.Patch(onCharEvent, prefix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(OnCharacterEventPrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.OnCharacterEvent not found — stealth break suppression inactive.");
            }
        }

        // Suppress the break-on-hit logic entirely in PvP — stealth runs its full duration.
        private static bool OnCharacterEventPrefix() => !ThePitState.IsDraftMode;

        private static void FixedUpdateNetworkPostfix(ShameleonEnterTheShadowAbility __instance) {
            _sidecars.GetValue(__instance, inst => new PvpShameleonEnterTheShadow(inst)).OnFixedUpdate();
        }

        internal static void Reset() {
            foreach (var a in UnityEngine.Object.FindObjectsOfType<ShameleonEnterTheShadowAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }
    }
}
