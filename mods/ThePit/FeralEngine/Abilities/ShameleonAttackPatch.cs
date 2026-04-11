using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal static class ShameleonAttackPatch {
        private static readonly ConditionalWeakTable<ShameleonAttackAbility, PvpShameleonAttackAbility> _sidecars = new();

        internal static void Apply(Harmony harmony) {
            var doHit = AccessTools.Method(typeof(ShameleonAttackAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonAttackAbility.DoHit not found — Shameleon attack PvP inactive.");
                return;
            }
            harmony.Patch(doHit, postfix: new HarmonyMethod(typeof(ShameleonAttackPatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(ShameleonAttackAbility __instance) =>
            _sidecars.GetValue(__instance, inst => new PvpShameleonAttackAbility(inst)).DoHit();
    }
}
