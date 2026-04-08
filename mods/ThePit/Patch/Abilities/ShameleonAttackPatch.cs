using System.Collections.Generic;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal static class ShameleonAttackPatch {
        internal static void Apply(Harmony harmony) {
            var doHit = AccessTools.Method(typeof(ShameleonAttackAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonAttackAbility.DoHit not found — Shameleon attack PvP inactive.");
                return;
            }
            harmony.Patch(doHit,
                postfix: new HarmonyMethod(typeof(ShameleonAttackPatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(ShameleonAttackAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }

            bool isLast = PvpDetector.IsLastComboPhase(__instance);
            var col = isLast ? __instance.lastHitCollider : __instance.normalHitCollider;
            if (col == null) { return; }

            var self = __instance.Stats;
            var hits = PvpDetector.Overlap(col, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var dmg = isLast ? __instance.damageAtLastHit : __instance.damageAtNormalHit;
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            var fx = PvpDetector.GetCurrentPhaseFX(__instance);

            foreach (var target in hits) {
                target.TakeBasicDamage(dmg, self,
                    PvpDetector.AttackDir(__instance, target),
                    __instance.ConnectedUserAction, fx);
            }
            PvpDetector.ToggleHasHit(__instance);
        }
    }
}
