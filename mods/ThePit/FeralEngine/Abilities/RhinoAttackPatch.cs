using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoAttackPatch {
        private static readonly ConditionalWeakTable<RhinoAttackAbility, PvpRhinoAttackAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            // DoHit is called during combat, well after Spawned() — create the sidecar lazily
            // here rather than in a Spawned() postfix, because FeralCore patches are applied
            // after champions spawn (at arena entry), so a Spawned() postfix would never fire.
            var doHit = AccessTools.Method(typeof(RhinoAttackAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoAttackAbility.DoHit not found — Rhino attack PvP inactive.");
                return;
            }
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(RhinoAttackPatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(RhinoAttackAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpRhinoAttackAbility(inst)).DoHit();
    }
}
