using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    internal static class RhinoAttackPatch {
        private static readonly ConditionalWeakTable<RhinoAttackAbility, PvpRhinoAttackAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(RhinoAttackAbility), "Spawned");
            var doHit = AccessTools.Method(typeof(RhinoAttackAbility), "DoHit");

            if (spawned == null || doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoAttackAbility.Spawned/DoHit not found — Rhino attack PvP inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(RhinoAttackPatch), nameof(SpawnedPostfix)));
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(RhinoAttackPatch), nameof(DoHitPostfix)));
        }

        private static void SpawnedPostfix(RhinoAttackAbility __instance) {
            _sidecars.Remove(__instance);
            _sidecars.Add(__instance, new PvpRhinoAttackAbility(__instance));
        }

        private static void DoHitPostfix(RhinoAttackAbility __instance) {
            if (_sidecars.TryGetValue(__instance, out var sidecar)) {
                sidecar.DoHit();
            }
        }
    }
}
