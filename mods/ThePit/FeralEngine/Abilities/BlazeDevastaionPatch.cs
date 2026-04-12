using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // Adds an arrival burst (instant damage + radial push) when Blaze teleports in PvP.
    internal static class BlazeDevastaionPatch {
        private static readonly ConditionalWeakTable<BlazeDevastation, PvpBlazeDevastation> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBlazeDevastation.Init();

            var spawned = AccessTools.Method(typeof(BlazeDevastation), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeDevastation.Spawned not found — arrival burst PvP inactive.");
                return;
            }

            var fixedUpdate = AccessTools.Method(typeof(BlazeDevastation), "FixedUpdateNetwork");
            if (fixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeDevastation.FixedUpdateNetwork not found — arrival burst inactive.");
                return;
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BlazeDevastaionPatch), nameof(SpawnedPostfix)));
            harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(BlazeDevastaionPatch), nameof(FixedUpdateNetworkPostfix)));
        }

        private static void SpawnedPostfix(BlazeDevastation __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBlazeDevastation(__instance));
        }

        private static void FixedUpdateNetworkPostfix(BlazeDevastation __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnFixedUpdate(); }
        }
    }
}
