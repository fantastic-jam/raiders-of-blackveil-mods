using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    internal static class RhinoStampedePatch {
        private static FieldInfo _grabbedEnemiesField;
        private static FieldInfo _grabbedThanThrowedField;
        private static MethodInfo _grabActorMethod;

        internal static void Apply(Harmony harmony) {
            _grabbedEnemiesField = AccessTools.Field(typeof(RhinoStampedeAbility), "_grabbedEnemies");
            _grabbedThanThrowedField = AccessTools.Field(typeof(RhinoStampedeAbility), "_grabbedThanThrowedEnemies");
            _grabActorMethod = AccessTools.Method(typeof(RhinoStampedeAbility), "GrabActor",
                new[] { typeof(StatsManager) });

            var detect = AccessTools.Method(typeof(RhinoStampedeAbility), "DetectEnemiesToGrab");
            if (detect == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: RhinoStampedeAbility.DetectEnemiesToGrab not found — Stampede PvP inactive.");
                return;
            }
            harmony.Patch(detect,
                postfix: new HarmonyMethod(typeof(RhinoStampedePatch), nameof(DetectPostfix)));
        }

        private static void DetectPostfix(RhinoStampedeAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            if (__instance.Runner?.IsServer != true) { return; }
            if (__instance.grabCollider == null) { return; }

            var self = __instance.Stats;
            var hits = PvpDetector.Overlap(__instance.grabCollider, excludes: new[] { self });
            if (hits.Count == 0) { return; }

            var grabbed = _grabbedEnemiesField?.GetValue(__instance) as List<StatsManager>;
            var throwed = _grabbedThanThrowedField?.GetValue(__instance) as List<StatsManager>;

            foreach (var target in hits) {
                if (!target.IsAlive) { continue; }
                if (grabbed != null && grabbed.Contains(target)) { continue; }
                if (throwed != null && throwed.Contains(target)) { continue; }

                target.TakeBasicDamage(__instance.damagePerHit, self,
                    PvpDetector.AttackDir(__instance, target),
                    __instance.ConnectedUserAction, __instance.ImpactEffects);

                // Attempt to grab the champion if not protected.
                if (target.CanBePushed && _grabActorMethod != null) {
                    _grabActorMethod.Invoke(__instance, new object[] { target });
                }
            }
        }
    }
}
