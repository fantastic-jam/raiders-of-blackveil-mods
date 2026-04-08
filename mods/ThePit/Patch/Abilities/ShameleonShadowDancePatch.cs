using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.Patch.Abilities {
    internal static class ShameleonShadowDancePatch {
        private static FieldInfo _damagePerAttackField;

        // Per-caster hit-count map for round-robin distribution across champions.
        // Outer key = caster ActorID, inner key = target ActorID, value = hits assigned so far.
        private static readonly Dictionary<int, Dictionary<int, int>> _hitCounts
            = new Dictionary<int, Dictionary<int, int>>();

        internal static void Reset() => _hitCounts.Clear();

        internal static void Apply(Harmony harmony) {
            _damagePerAttackField = AccessTools.Field(typeof(ShameleonShadowDanceAbility), "damagePerAttack");
            if (_damagePerAttackField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.damagePerAttack not found — Shadow Dance PvP inactive.");
            }

            var letsDance = AccessTools.Method(typeof(ShameleonShadowDanceAbility), "LetsDance");
            if (letsDance == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.LetsDance not found — Shadow Dance PvP inactive.");
                return;
            }
            harmony.Patch(letsDance,
                prefix: new HarmonyMethod(typeof(ShameleonShadowDancePatch), nameof(LetsDancePrefix)));
        }

        // Replaces LetsDance() in PvP mode.
        // ref bool __result sets the return value; return false skips the original.
        private static bool LetsDancePrefix(ShameleonShadowDanceAbility __instance, ref bool __result) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return true; }
            if (__instance.Runner?.IsServer != true) { return true; }
            if (_damagePerAttackField == null) { return true; }

            var self = __instance.Stats;
            var targets = PvpDetector.OverlapSphere(
                __instance.transform.position, __instance.areaRadius,
                excludes: new[] { self });

            if (targets.Count == 0) {
                __result = false;
                return false;
            }

            int casterId = self.ActorID;
            if (!_hitCounts.TryGetValue(casterId, out var hitCounts)) {
                hitCounts = new Dictionary<int, int>();
                _hitCounts[casterId] = hitCounts;
            }

            // Round-robin: pick the living champion with the fewest hits so far.
            StatsManager best = null;
            int minHits = int.MaxValue;
            foreach (var t in targets) {
                if (!t.IsAlive) { continue; }
                hitCounts.TryGetValue(t.ActorID, out int h);
                if (h < minHits) { minHits = h; best = t; }
            }

            if (best == null) {
                __result = false;
                return false;
            }

            var dmg = (DamageDescriptor)_damagePerAttackField.GetValue(__instance);
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            best.TakeBasicDamage(dmg, self,
                PvpDetector.AttackDir(__instance, best),
                __instance.ConnectedUserAction, __instance.ImpactEffects);

            hitCounts[best.ActorID] = minHits + 1;

            // Count total hits to know when to reset for the next activation.
            int totalHits = 0;
            foreach (var v in hitCounts.Values) { totalHits += v; }
            if (totalHits >= __instance.numberOfAttacks) {
                _hitCounts.Remove(casterId);
            }

            PvpDetector.ToggleHasHit(__instance);
            __result = true;
            return false;
        }
    }
}
