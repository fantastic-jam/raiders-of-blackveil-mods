using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // Adds an arrival burst (instant damage + radial push) when Blaze teleports in PvP.
    internal static class BlazeDevastaionPatch {
        private static readonly ConditionalWeakTable<BlazeDevastation, PvpBlazeDevastation> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            PvpBlazeDevastation.Init();

            var fixedUpdate = AccessTools.Method(typeof(BlazeDevastation), "FixedUpdateNetwork");
            if (fixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeDevastation.FixedUpdateNetwork not found — arrival burst inactive.");
                return;
            }
            harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(BlazeDevastaionPatch), nameof(FixedUpdateNetworkPostfix)));
        }

        private static void FixedUpdateNetworkPostfix(BlazeDevastation __instance) {
            _sidecars.GetValue(__instance, inst => new PvpBlazeDevastation(inst)).OnFixedUpdate();
        }
    }
}
