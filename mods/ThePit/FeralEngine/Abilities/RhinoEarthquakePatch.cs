using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoEarthquakePatch {
        private static readonly ConditionalWeakTable<RhinoEarthquakeAbility, PvpRhinoEarthquakeAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpRhinoEarthquakeAbility.WavePosGetter == null ||
                PvpRhinoEarthquakeAbility.WaveStartPosField == null ||
                PvpRhinoEarthquakeAbility.WaveDirectionField == null ||
                PvpRhinoEarthquakeAbility.HittedEnemiesField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility fields not found — Earthquake PvP inactive.");
            }
            var fun = AccessTools.Method(typeof(RhinoEarthquakeAbility), "FixedUpdateNetwork");
            if (fun == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoEarthquakeAbility.FixedUpdateNetwork not found — Earthquake PvP inactive.");
                return;
            }
            harmony.Patch(fun, postfix: new HarmonyMethod(typeof(RhinoEarthquakePatch), nameof(FunPostfix)));
        }

        private static void FunPostfix(RhinoEarthquakeAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpRhinoEarthquakeAbility(inst)).OnFixedUpdate();
    }
}
