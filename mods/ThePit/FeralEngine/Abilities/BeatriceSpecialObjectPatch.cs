using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class BeatriceSpecialObjectPatch {
        private static readonly ConditionalWeakTable<BeatriceSpecialObject, PvpBeatriceSpecialObject> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var flowerEffect = AccessTools.Method(typeof(BeatriceSpecialObject), "FlowerEffect");
            if (flowerEffect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceSpecialObject.FlowerEffect not found — Lotus Flower PvP fix inactive.");
                return;
            }
            harmony.Patch(flowerEffect, prefix: new HarmonyMethod(typeof(BeatriceSpecialObjectPatch), nameof(FlowerEffectPrefix)));
        }

        private static bool FlowerEffectPrefix(BeatriceSpecialObject __instance) {
            return _sidecars.GetValue(__instance, inst => new PvpBeatriceSpecialObject(inst)).FlowerEffect();
        }
    }
}
