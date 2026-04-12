using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Restricts Blaze's heat aura critical-chance buff to the caster only.
    // Also burns champion opponents inside the aura at the same rate as normal enemies.
    internal static class BlazeSpecialAreaPatch {
        private static readonly ConditionalWeakTable<BlazeSpecialArea, PvpBlazeSpecialArea> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBlazeSpecialArea.Init();

            var spawned = AccessTools.Method(typeof(BlazeSpecialArea), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.Spawned not found — Heat Aura PvP fix inactive.");
                return;
            }

            var updateAura = AccessTools.Method(typeof(BlazeSpecialArea), "UpdateAuraEffect", new[] { typeof(bool) });
            if (updateAura == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.UpdateAuraEffect not found — Heat Aura buff fix inactive.");
            }

            var fixedUpdate = AccessTools.Method(typeof(BlazeSpecialArea), "FixedUpdateNetwork");
            if (fixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.FixedUpdateNetwork not found — champion burn inactive.");
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BlazeSpecialAreaPatch), nameof(SpawnedPostfix)));
            if (updateAura != null) {
                harmony.Patch(updateAura, prefix: new HarmonyMethod(typeof(BlazeSpecialAreaPatch), nameof(UpdateAuraEffectPrefix)));
            }
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(BlazeSpecialAreaPatch), nameof(FixedUpdateNetworkPostfix)));
            }
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<BlazeSpecialArea>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(BlazeSpecialArea __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBlazeSpecialArea(__instance));
        }

        private static bool UpdateAuraEffectPrefix(BlazeSpecialArea __instance, bool checkWhoIsInside) {
            if (!ThePitState.IsDraftMode) { return true; }
            if (!_proxies.TryGetValue(__instance, out var proxy)) { return true; }
            return proxy.UpdateAuraEffect(checkWhoIsInside);
        }

        private static void FixedUpdateNetworkPostfix(BlazeSpecialArea __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnFixedUpdate(); }
        }
    }
}
