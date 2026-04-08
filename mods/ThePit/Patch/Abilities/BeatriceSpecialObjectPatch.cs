using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    // In the original, FlowerEffect() calls _hitDetector.OverlapSphere() and calls
    // AddArmorPlate() on EVERY champion in range.  In PvP only the caster's champion
    // should receive the armor plate.
    internal static class BeatriceSpecialObjectPatch {
        internal static void Apply(Harmony harmony) {
            var flowerEffect = AccessTools.Method(typeof(BeatriceSpecialObject), "FlowerEffect");
            if (flowerEffect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceSpecialObject.FlowerEffect not found — Lotus Flower PvP fix inactive.");
                return;
            }
            harmony.Patch(flowerEffect,
                prefix: new HarmonyMethod(typeof(BeatriceSpecialObjectPatch), nameof(FlowerEffectPrefix)));
        }

        private static bool FlowerEffectPrefix(BeatriceSpecialObject __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return true; }
            if (__instance.Runner?.IsServer != true) { return true; }

            // Apply armor plate only to the caster's champion.
            var caster = __instance._charRef;
            if (caster != null) {
                caster.Stats?.Protection?.AddArmorPlate();
            }

            return false; // skip original (which would buff all champions in range)
        }
    }
}
