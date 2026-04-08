using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal static class ShameleonShadowStrikePatch {
        internal static void Apply(Harmony harmony) {
            var doHit = AccessTools.Method(typeof(ShameleonShadowStrikeAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowStrikeAbility.DoHit not found — Shadow Strike PvP inactive.");
                return;
            }
            harmony.Patch(doHit,
                postfix: new HarmonyMethod(typeof(ShameleonShadowStrikePatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(ShameleonShadowStrikeAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }

            var self = __instance.Stats;
            var excludes = new[] { self };
            bool anyHit = false;

            if (__instance.hitCollider1 != null) {
                var col1 = __instance.hitCollider1;
                var hits1 = PvpDetector.OverlapBox(
                    col1.transform.TransformPoint(col1.center),
                    Vector3.Scale(col1.size * 0.5f, AbsScale(col1.transform.lossyScale)),
                    col1.transform.rotation,
                    excludes: excludes);
                var dmg1 = __instance.damage;
                dmg1.blessedAttack = self.IsBlessed;
                dmg1.furyAttack = self.HasFury;
                foreach (var target in hits1) {
                    target.TakeBasicDamage(dmg1, self,
                        PvpDetector.AttackDir(__instance, target),
                        __instance.ConnectedUserAction, __instance.ImpactEffects);
                    anyHit = true;
                }
            }

            if (__instance.hitCollider2 != null) {
                var col2 = __instance.hitCollider2;
                var hits2 = PvpDetector.OverlapBox(
                    col2.transform.TransformPoint(col2.center),
                    Vector3.Scale(col2.size * 0.5f, AbsScale(col2.transform.lossyScale)),
                    col2.transform.rotation,
                    excludes: excludes);
                var dmg2 = __instance.damage;
                dmg2.blessedAttack = self.IsBlessed;
                dmg2.furyAttack = self.HasFury;
                foreach (var target in hits2) {
                    target.TakeBasicDamage(dmg2, self,
                        PvpDetector.AttackDir(__instance, target),
                        __instance.ConnectedUserAction, __instance.ImpactEffects);
                    anyHit = true;
                }
            }

            if (anyHit) {
                PvpDetector.ToggleHasHit(__instance);
            }
        }

        private static UnityEngine.Vector3 AbsScale(UnityEngine.Vector3 s) =>
            new UnityEngine.Vector3(
                Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }
}
