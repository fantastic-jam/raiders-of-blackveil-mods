using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Input;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    internal static class SunStrikeAreaPatch {
        private static MethodInfo _hasHitGetter;
        private static MethodInfo _hasHitSetter;

        internal static void Apply(Harmony harmony) {
            _hasHitGetter = AccessTools.PropertyGetter(typeof(SunStrikeArea), "_hasHit");
            _hasHitSetter = AccessTools.PropertySetter(typeof(SunStrikeArea), "_hasHit");

            if (_hasHitGetter == null || _hasHitSetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: SunStrikeArea._hasHit not found — Sun Strike PvP may double-fire.");
            }

            var dmgCheck = AccessTools.Method(typeof(SunStrikeArea), "DamageCheck");
            if (dmgCheck == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: SunStrikeArea.DamageCheck not found — Sun Strike PvP inactive.");
                return;
            }
            harmony.Patch(dmgCheck,
                postfix: new HarmonyMethod(typeof(SunStrikeAreaPatch), nameof(DamageCheckPostfix)));
        }

        private static void DamageCheckPostfix(SunStrikeArea __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }

            // Guard: only fire once per area instance.
            if (_hasHitGetter != null) {
                var hasHit = (Fusion.NetworkBool)_hasHitGetter.Invoke(__instance, null);
                if (hasHit) { return; }
            }

            var caster = __instance.Caster;
            if (caster == null || __instance.HitCollider == null) { return; }

            var self = caster.Stats;
            if (self == null) { return; }

            var hits = PvpDetector.Overlap(__instance.HitCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = __instance.Damage;
            foreach (var target in hits) {
                target.TakeBasicDamage(dmg, self,
                    PvpDetector.AttackDir(__instance, target), UserAction.Special);
            }

            // Mark as fired so we don't apply champion damage again on subsequent ticks.
            _hasHitSetter?.Invoke(__instance, new object[] { (Fusion.NetworkBool)true });
        }
    }
}
