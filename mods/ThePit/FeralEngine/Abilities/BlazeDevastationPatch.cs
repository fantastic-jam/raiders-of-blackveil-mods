using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Adds an arrival burst (instant damage + radial push) when Blaze teleports in PvP.
    internal static class BlazeDevastationPatch {
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

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BlazeDevastationPatch), nameof(SpawnedPostfix)));
            harmony.Patch(fixedUpdate, postfix: new HarmonyMethod(typeof(BlazeDevastationPatch), nameof(FixedUpdateNetworkPostfix)));

            // Backfill proxies for instances that were already spawned before Apply() ran
            // (Spawned() fires at match-start upgrades, before FeralCore patches are applied).
            foreach (var a in Object.FindObjectsOfType<BlazeDevastation>()) {
                InitProxy(a);
            }
        }

        private static void InitProxy(BlazeDevastation inst) {
            _proxies.Remove(inst);
            _proxies.Add(inst, new PvpBlazeDevastation(inst));
        }

        private static void SpawnedPostfix(BlazeDevastation __instance) {
            InitProxy(__instance);
        }

        private static void FixedUpdateNetworkPostfix(BlazeDevastation __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnFixedUpdate(); }
        }
    }
}
