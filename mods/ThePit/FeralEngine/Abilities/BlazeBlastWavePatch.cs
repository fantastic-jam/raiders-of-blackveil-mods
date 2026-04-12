using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // PvP extension for BlazeBlastWave.
    // The original CollectAndGrabEnemies / PushAwayEnemies / DamageEnemies iterate
    // NetworkEnemyBase.AllEnemies, which is empty in the arena. We add champion
    // detection on top via postfix without touching the original flow.
    internal static class BlazeBlastWavePatch {
        private static readonly ConditionalWeakTable<BlazeBlastWave, PvpBlazeBlastWave> _proxies = new();

        internal static void Apply(Harmony harmony) {
            PvpBlazeBlastWave.Init();

            var spawned = AccessTools.Method(typeof(BlazeBlastWave), "Spawned");
            if (spawned == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave.Spawned not found — Blast Wave PvP inactive.");
                return;
            }

            var collect = AccessTools.Method(typeof(BlazeBlastWave), "CollectAndGrabEnemies");
            if (collect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave.CollectAndGrabEnemies not found — Blast Wave collect PvP inactive.");
            }

            var push = AccessTools.Method(typeof(BlazeBlastWave), "PushAwayEnemies");
            if (push == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave.PushAwayEnemies not found — Blast Wave push PvP inactive.");
            }

            var damage = AccessTools.Method(typeof(BlazeBlastWave), "DamageEnemies");
            if (damage == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeBlastWave.DamageEnemies not found — Blast Wave damage PvP inactive.");
            }

            harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(SpawnedPostfix)));
            if (collect != null) {
                harmony.Patch(collect, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(CollectPostfix)));
            }
            if (push != null) {
                harmony.Patch(push, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(PushPostfix)));
            }
            if (damage != null) {
                harmony.Patch(damage, postfix: new HarmonyMethod(typeof(BlazeBlastWavePatch), nameof(DamagePostfix)));
            }
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<BlazeBlastWave>()) {
                if (_proxies.TryGetValue(a, out var proxy)) { proxy.Reset(); }
            }
        }

        private static void SpawnedPostfix(BlazeBlastWave __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpBlazeBlastWave(__instance));
        }

        private static void CollectPostfix(BlazeBlastWave __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnCollect(); }
        }

        private static void PushPostfix(BlazeBlastWave __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnPush(); }
        }

        private static void DamagePostfix(BlazeBlastWave __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.OnDamage(); }
        }
    }
}
