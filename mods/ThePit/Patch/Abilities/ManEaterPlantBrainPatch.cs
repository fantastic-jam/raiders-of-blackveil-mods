using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // The original HitEnemiesInArch uses _hitDetector.DoDetection() (AllTargets) which
    // misses the Player layer.  We add champion detection on top via a postfix.
    internal static class ManEaterPlantBrainPatch {
        internal static void Apply(Harmony harmony) {
            var hitArch = AccessTools.Method(typeof(ManEaterPlantBrain), "HitEnemiesInArch");
            if (hitArch == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ManEaterPlantBrain.HitEnemiesInArch not found — Man-Eater Plant PvP inactive.");
                return;
            }
            harmony.Patch(hitArch,
                postfix: new HarmonyMethod(typeof(ManEaterPlantBrainPatch), nameof(HitEnemiesInArchPostfix)));
        }

        private static void HitEnemiesInArchPostfix(ManEaterPlantBrain __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (__instance.dealDamageCollider == null) { return; }

            var creator = __instance._creator;
            var excludes = creator != null ? new[] { creator } : null;
            var hits = PvpDetector.Overlap(__instance.dealDamageCollider, excludes: excludes);
            if (hits.Count == 0) { return; }

            // Angle filter — same as original (dot-product with plant's forward direction).
            float cosAngle = Mathf.Cos(__instance.dealDamageAngle * Mathf.Deg2Rad * 0.5f);
            var forward = __instance.transform.forward;
            var origin = __instance.transform.position;

            var dmg = __instance.damage;
            foreach (var target in hits) {
                var dir = target.transform.position - origin;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.001f || Vector3.Dot(forward, dir.normalized) < cosAngle) { continue; }

                target.TakeBasicDamage(dmg, creator,
                    PvpDetector.AttackDir(__instance, target),
                    RR.Game.Input.UserAction.Offensive, __instance.ImpactEffects);
            }
        }
    }
}
