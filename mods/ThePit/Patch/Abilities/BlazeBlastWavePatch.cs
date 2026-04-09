using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // PvP extension for BlazeBlastWave.
    // The original CollectAndGrabEnemies / PushAwayEnemies / DamageEnemies iterate
    // NetworkEnemyBase.AllEnemies, which is empty in the arena. We add champion
    // detection on top via postfix without touching the original flow.
    internal static class BlazeBlastWavePatch {
        private static readonly ConditionalWeakTable<BlazeBlastWave, PvpBlazeBlastWave> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var collect = AccessTools.Method(typeof(BlazeBlastWave), "CollectAndGrabEnemies");
            var push = AccessTools.Method(typeof(BlazeBlastWave), "PushAwayEnemies");
            var damage = AccessTools.Method(typeof(BlazeBlastWave), "DamageEnemies");

            if (collect == null || push == null || damage == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave methods not found — Blast Wave PvP inactive.");
                return;
            }

            harmony.Patch(collect, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(CollectPostfix)));
            harmony.Patch(push, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(PushPostfix)));
            harmony.Patch(damage, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(DamagePostfix)));
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<BlazeBlastWave>()) {
                if (_sidecars.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }

        private static void CollectPostfix(BlazeBlastWave __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBlazeBlastWave(inst)).OnCollect();

        private static void PushPostfix(BlazeBlastWave __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBlazeBlastWave(inst)).OnPush();

        private static void DamagePostfix(BlazeBlastWave __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpBlazeBlastWave(inst)).OnDamage();
    }
}
