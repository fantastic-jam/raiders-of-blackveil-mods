using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.FeralEngine.Abilities {
    // In PvP: Enter the Shadow grants immunity + triple move speed instead of vanilla invisible.
    // AddInvisible is blocked during draft so stealth is purely immune-based.
    internal static class ShameleonEnterTheShadowPatch {
        private static readonly ConditionalWeakTable<ShameleonEnterTheShadowAbility, PvpShameleonEnterTheShadow> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            PvpShameleonEnterTheShadow.Init();

            // Block vanilla AddInvisible during draft — stealth is immune-only, not invisible.
            var addInvisible = AccessTools.Method(typeof(Health), "AddInvisible");
            if (addInvisible != null) {
                harmony.Patch(addInvisible,
                    prefix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(AddInvisiblePrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.AddInvisible not found — stealth invisible suppression inactive.");
            }

            var fixedUpdate = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(FixedUpdateNetworkPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.FixedUpdateNetwork not found — stealth buff inactive.");
            }
        }

        // Returns false (blocks AddInvisible) whenever the match is active.
        // Vanilla's paired RemoveInvisible calls (timer expiry, hit events) are harmless no-ops
        // since the counter was never incremented.
        private static bool AddInvisiblePrefix() => !ThePitState.IsDraftMode;

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
