using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // In PvP: Enter the Shadow grants immunity + triple move speed; attacking breaks stealth normally.
    internal static class ShameleonEnterTheShadowPatch {
        private static readonly ConditionalWeakTable<ShameleonEnterTheShadowAbility, PvpShameleonEnterTheShadow> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            PvpShameleonEnterTheShadow.Init();

            var fixedUpdate = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(FixedUpdateNetworkPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.FixedUpdateNetwork not found — stealth buff inactive.");
            }
        }

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
