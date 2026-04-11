using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoSpinPatch {
        private static readonly ConditionalWeakTable<RhinoSpinAbility, PvpRhinoSpinAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpRhinoSpinAbility.DamagePerCycleField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.damagePerCycle not found — Spin PvP inactive.");
            }
            var doHit = AccessTools.Method(typeof(RhinoSpinAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoSpinAbility.DoHit not found — Spin PvP inactive.");
                return;
            }
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(RhinoSpinPatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(RhinoSpinAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpRhinoSpinAbility(inst)).DoHit();
    }
}
