using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class BeatriceSpecialObjectPatch {
        private static readonly ConditionalWeakTable<BeatriceSpecialObject, PvpBeatriceSpecialObject> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(BeatriceSpecialObject), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceSpecialObject.Spawned not found — Lotus Flower PvP fix inactive.");
                return;
            }
            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BeatriceSpecialObjectPatch), nameof(SpawnedPostfix)));

            var flowerEffect = AccessTools.Method(typeof(BeatriceSpecialObject), "FlowerEffect");
            if (flowerEffect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceSpecialObject.FlowerEffect not found — Lotus Flower PvP fix inactive.");
                return;
            }
            harmony.Patch(flowerEffect, prefix: new HarmonyMethod(typeof(BeatriceSpecialObjectPatch), nameof(FlowerEffectPrefix)));
        }

        private static void SpawnedPostfix(BeatriceSpecialObject __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBeatriceSpecialObject(__instance));
        }

        private static bool FlowerEffectPrefix(BeatriceSpecialObject __instance) =>
            !_proxies.TryGetValue(__instance, out var s) || s.FlowerEffect();
    }
}
