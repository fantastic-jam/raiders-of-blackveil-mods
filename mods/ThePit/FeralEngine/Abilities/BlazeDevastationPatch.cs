using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Adds an arrival burst (instant damage + radial push) when Blaze teleports in PvP.
    // Also disables Recastable in draft mode so the ultimate goes through the normal cooldown
    // instead of allowing infinite recasts whenever JustGotKill is true.
    internal static class BlazeDevastationPatch {
        private static readonly ConditionalWeakTable<BlazeDevastation, PvpBlazeDevastation> _proxies = new();
        private static FieldInfo _heatTraitField;

        internal static void Apply(Harmony harmony) {
            PvpBlazeDevastation.Init();

            _heatTraitField = AccessTools.Field(typeof(BlazeDevastation), "_heatTrait");
            if (_heatTraitField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeDevastation._heatTrait not found — ult cooldown client-sync inactive.");
            }

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

            var recastable = AccessTools.PropertyGetter(typeof(BlazeDevastation), "Recastable");
            if (recastable != null) {
                harmony.Patch(recastable, prefix: new HarmonyMethod(typeof(BlazeDevastationPatch), nameof(RecastablePrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeDevastation.Recastable not found — ultimate cooldown fix inactive.");
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

        private static bool RecastablePrefix(BlazeDevastation __instance, ref bool __result) {
            if (!ThePitState.IsDraftMode) { return true; }
            __result = false;
            // Reset the networked JustGotKill so unmodded clients also compute Recastable = false.
            if (_heatTraitField != null && __instance.Runner?.IsServer == true) {
                var heatTrait = _heatTraitField.GetValue(__instance) as BlazeHeatTrait;
                heatTrait?.ResetKillFlag();
            }
            return false;
        }

        private static void SpawnedPostfix(BlazeDevastation __instance) {
            InitProxy(__instance);
        }

        private static void FixedUpdateNetworkPostfix(BlazeDevastation __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnFixedUpdate(); }
        }
    }
}
