using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpShameleonShadowDanceAbility {
        private static FieldInfo _damagePerAttackField;
        private static FieldInfo _spawnedShadowCountField;

        private readonly ShameleonShadowDanceAbility _inst;
        // targetActorId → hits assigned this dance session
        private readonly Dictionary<int, int> _hitCounts = new();

        internal static void Init() {
            _damagePerAttackField = AccessTools.Field(typeof(ShameleonShadowDanceAbility), "damagePerAttack");
            if (_damagePerAttackField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility.damagePerAttack not found — Shadow Dance PvP inactive.");
            }

            _spawnedShadowCountField = AccessTools.Field(typeof(ShameleonShadowDanceAbility), "_spawnedShadowCount");
            if (_spawnedShadowCountField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonShadowDanceAbility._spawnedShadowCount not found — Shadow Dance may not exit.");
            }
        }

        internal PvpShameleonShadowDanceAbility(ShameleonShadowDanceAbility inst) { _inst = inst; }

        // Returns false to let vanilla run; returns true and sets result to block vanilla.
        internal bool LetsDance(ref bool result) {
            if (_inst.Runner?.IsServer != true) { return true; }
            if (_damagePerAttackField == null) { return true; }

            int shadowCount = (int)(_spawnedShadowCountField?.GetValue(_inst) ?? 0);
            _spawnedShadowCountField?.SetValue(_inst, shadowCount + 1);

            var self = _inst.Stats;
            var targets = PvpDetector.OverlapSphere(_inst.transform.position, _inst.areaRadius, excludes: new[] { self });
            if (targets.Count == 0) {
                result = false;
                return false;
            }

            StatsManager best = null;
            int minHits = int.MaxValue;
            foreach (var t in targets) {
                if (!t.IsAlive) { continue; }
                _hitCounts.TryGetValue(t.ActorID, out int h);
                if (h < minHits) { minHits = h; best = t; }
            }
            if (best == null) {
                result = false;
                return false;
            }

            var dmg = (DamageDescriptor)_damagePerAttackField.GetValue(_inst);
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            best.TakeBasicDamage(dmg, self,
                PvpDetector.AttackDir(_inst, best),
                _inst.ConnectedUserAction, _inst.ImpactEffects);

            _hitCounts[best.ActorID] = minHits + 1;

            if (shadowCount + 1 >= _inst.numberOfAttacks) { _hitCounts.Clear(); }

            PvpDetector.ToggleHasHit(_inst);
            result = true;
            return false;
        }

        internal void Reset() { _hitCounts.Clear(); }
    }
}
