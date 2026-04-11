using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class RhinoShieldsUpPatch {
        private static readonly ConditionalWeakTable<RhinoShieldsUpAbility, PvpRhinoShieldsUpAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            if (PvpRhinoShieldsUpAbility.BaseDamageField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.baseDamage not found — Shields Up PvP inactive.");
            }
            var hitEnemies = AccessTools.Method(typeof(RhinoShieldsUpAbility), "HitEnemies", new[] { typeof(float) });
            if (hitEnemies == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoShieldsUpAbility.HitEnemies not found — Shields Up PvP inactive.");
                return;
            }
            harmony.Patch(hitEnemies, postfix: new HarmonyMethod(typeof(RhinoShieldsUpPatch), nameof(HitEnemiesPostfix)));
        }

        private static void HitEnemiesPostfix(RhinoShieldsUpAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpRhinoShieldsUpAbility(inst)).HitEnemies();
    }
}
