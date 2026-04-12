using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // Augmentation pattern: vanilla DoHit runs for PvE; we layer PvP detection on top in a postfix.
    internal static class ShameleonAttackPatch {
        private static readonly ConditionalWeakTable<ShameleonAttackAbility, PvpShameleonAttackAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(ShameleonAttackAbility), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ShameleonAttackPatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonAttackAbility.Spawned not found — Shameleon attack proxy inactive.");
            }

            var doHit = AccessTools.Method(typeof(ShameleonAttackAbility), "DoHit");
            if (doHit != null) {
                harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(ShameleonAttackPatch), nameof(DoHitPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonAttackAbility.DoHit not found — Shameleon attack PvP inactive.");
            }
        }

        private static void SpawnedPostfix(ShameleonAttackAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpShameleonAttackAbility(__instance));
        }

        private static void DoHitPostfix(ShameleonAttackAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DoHit(); }
        }
    }
}
