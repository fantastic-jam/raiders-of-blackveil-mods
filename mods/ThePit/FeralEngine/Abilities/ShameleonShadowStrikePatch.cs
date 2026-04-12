using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    // Augmentation pattern: vanilla DoHit runs for PvE; we layer PvP detection on top in a postfix.
    internal static class ShameleonShadowStrikePatch {
        private static readonly ConditionalWeakTable<ShameleonShadowStrikeAbility, PvpShameleonShadowStrikeAbility> _proxies = new();

        internal static void Apply(Harmony harmony) {
            var spawned = AccessTools.Method(typeof(ShameleonShadowStrikeAbility), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned, postfix: new HarmonyMethod(typeof(ShameleonShadowStrikePatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowStrikeAbility.Spawned not found — Shadow Strike proxy inactive.");
            }

            var doHit = AccessTools.Method(typeof(ShameleonShadowStrikeAbility), "DoHit");
            if (doHit != null) {
                harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(ShameleonShadowStrikePatch), nameof(DoHitPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowStrikeAbility.DoHit not found — Shadow Strike PvP inactive.");
            }
        }

        private static void SpawnedPostfix(ShameleonShadowStrikeAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpShameleonShadowStrikeAbility(__instance));
        }

        private static void DoHitPostfix(ShameleonShadowStrikeAbility __instance) {
            if (_proxies.TryGetValue(__instance, out var proxy)) { proxy.DoHit(); }
        }
    }
}
