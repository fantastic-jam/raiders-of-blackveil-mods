using System.Collections.Generic;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal static class RhinoAttackPatch {
        internal static void Apply(Harmony harmony) {
            var doHit = AccessTools.Method(typeof(RhinoAttackAbility), "DoHit");
            if (doHit == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoAttackAbility.DoHit not found — Rhino attack PvP inactive.");
                return;
            }
            harmony.Patch(doHit,
                postfix: new HarmonyMethod(typeof(RhinoAttackPatch), nameof(DoHitPostfix)));
        }

        private static void DoHitPostfix(RhinoAttackAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }

            bool isLast = PvpDetector.IsLastComboPhase(__instance);
            var self = __instance.Stats;
            var excludes = new[] { self };

            var hits = new List<StatsManager>();

            // Collect all hit-collider results (deduplicated).
            if (isLast) {
                if (__instance.lastHitCollider1 != null) {
                    hits.AddRange(PvpDetector.Overlap(__instance.lastHitCollider1, excludes: excludes));
                }
            } else {
                if (__instance.normalHitCollider1 != null) {
                    foreach (var h in PvpDetector.Overlap(__instance.normalHitCollider1, excludes: excludes)) {
                        if (!hits.Contains(h)) { hits.Add(h); }
                    }
                }
                if (__instance.normalHitCollider2 != null) {
                    foreach (var h in PvpDetector.Overlap(__instance.normalHitCollider2, excludes: excludes)) {
                        if (!hits.Contains(h)) { hits.Add(h); }
                    }
                }
            }

            if (hits.Count == 0) { return; }

            var dmg = isLast ? __instance.damageAtLastHit : __instance.damageAtNormalHit;
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            var fx = PvpDetector.GetCurrentPhaseFX(__instance);

            foreach (var target in hits) {
                var dir = PvpDetector.AttackDir(__instance, target);
                target.TakeBasicDamage(dmg, self, dir, __instance.ConnectedUserAction, fx);

                // Last hit push force.
                if (isLast && target.CanBePushed && target.Character != null) {
                    var pushDir = dir;
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude > 0.001f) {
                        target.Character.AddPushForce(pushDir.normalized * __instance.pushForceLastHit);
                    }
                }
            }
            PvpDetector.ToggleHasHit(__instance);
        }
    }
}
