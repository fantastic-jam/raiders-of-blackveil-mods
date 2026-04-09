using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    internal static class ShameleonShadowStrikePatch {
        private static readonly ConditionalWeakTable<ShameleonShadowStrikeAbility, PvpShameleonShadowStrikeAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var doHit = AccessTools.Method(typeof(ShameleonShadowStrikeAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowStrikeAbility.DoHit not found — Shadow Strike PvP inactive.");
                return;
            }
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(ShameleonShadowStrikePatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(ShameleonShadowStrikeAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpShameleonShadowStrikeAbility(inst)).DoHit();
    }
}
