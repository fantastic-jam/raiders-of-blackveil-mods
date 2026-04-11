using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // In PvP: Enter the Shadow grants immune + 3× speed instead of vanilla invisible.
    // AddInvisible is blocked so the stealth material never shows.
    // Each public override on ShameleonEnterTheShadowAbility is patched and forwarded
    // to the sidecar so it reacts to the same events vanilla does.
    internal static class ShameleonEnterTheShadowPatch {
        private static readonly ConditionalWeakTable<ShameleonEnterTheShadowAbility, PvpShameleonEnterTheShadow> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            PvpShameleonEnterTheShadow.Init();

            var addInvisible = AccessTools.Method(typeof(Health), "AddInvisible");
            if (addInvisible != null) {
                harmony.Patch(addInvisible,
                    prefix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(AddInvisiblePrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.AddInvisible not found — stealth invisible suppression inactive.");
            }

            var spawned = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned,
                    postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.Spawned not found — stealth buff inactive.");
            }

            var fixedUpdate = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate,
                    postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(FixedUpdateNetworkPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.FixedUpdateNetwork not found — stealth buff inactive.");
            }

            var onCharEvent = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "OnCharacterEvent");
            if (onCharEvent != null) {
                harmony.Patch(onCharEvent,
                    postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(OnCharacterEventPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.OnCharacterEvent not found — stealth event handling inactive.");
            }
        }

        private static bool AddInvisiblePrefix() => !ThePitState.IsDraftMode;

        private static void SpawnedPostfix(ShameleonEnterTheShadowAbility __instance) {
            _sidecars.Remove(__instance);
            _sidecars.Add(__instance, new PvpShameleonEnterTheShadow(__instance));
        }

        private static void FixedUpdateNetworkPostfix(ShameleonEnterTheShadowAbility __instance) {
            if (_sidecars.TryGetValue(__instance, out var s)) { s.OnFixedUpdate(); }
        }

        private static void OnCharacterEventPostfix(ShameleonEnterTheShadowAbility __instance) {
            if (_sidecars.TryGetValue(__instance, out var s)) { s.OnCharacterEvent(); }
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<ShameleonEnterTheShadowAbility>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }
    }
}
