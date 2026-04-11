using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoStampedePatch {
        private static readonly ConditionalWeakTable<RhinoStampedeAbility, PvpRhinoStampedeAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var detect = AccessTools.Method(typeof(RhinoStampedeAbility), "DetectEnemiesToGrab");
            if (detect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility.DetectEnemiesToGrab not found — Stampede PvP inactive.");
                return;
            }
            harmony.Patch(detect, postfix: new HarmonyMethod(typeof(RhinoStampedePatch), nameof(DetectPostfix)));
        }

        private static void DetectPostfix(RhinoStampedeAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpRhinoStampedeAbility(inst)).DetectEnemies();
    }
}
