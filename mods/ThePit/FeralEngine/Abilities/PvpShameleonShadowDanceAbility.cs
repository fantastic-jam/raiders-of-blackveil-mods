using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpShameleonShadowDanceAbility {
        internal static readonly FieldInfo DamagePerAttackField = AccessTools.Field(typeof(ShameleonShadowDanceAbility), "damagePerAttack");
        internal static readonly FieldInfo SpawnedShadowCountField = AccessTools.Field(typeof(ShameleonShadowDanceAbility), "_spawnedShadowCount");

        private readonly ShameleonShadowDanceAbility _inst;
        // targetActorId → hits assigned this dance session
        private readonly Dictionary<int, int> _hitCounts = new();

        internal PvpShameleonShadowDanceAbility(ShameleonShadowDanceAbility inst) { _inst = inst; }

        // Returns (skipOriginal, originalReturnValue).
        internal (bool skip, bool result) LetsDance() {
            if (_inst.Runner?.IsServer != true) { return (false, false); }
            if (DamagePerAttackField == null) { return (false, false); }

            int shadowCount = (int)(SpawnedShadowCountField?.GetValue(_inst) ?? 0);
            SpawnedShadowCountField?.SetValue(_inst, shadowCount + 1);

            var self = _inst.Stats;
            var targets = PvpDetector.OverlapSphere(_inst.transform.position, _inst.areaRadius, excludes: new[] { self });
            if (targets.Count == 0) { return (true, false); }

            StatsManager best = null;
            int minHits = int.MaxValue;
            foreach (var t in targets) {
                if (!t.IsAlive) { continue; }
                _hitCounts.TryGetValue(t.ActorID, out int h);
                if (h < minHits) { minHits = h; best = t; }
            }
            if (best == null) { return (true, false); }

            var dmg = (DamageDescriptor)DamagePerAttackField.GetValue(_inst);
            dmg.blessedAttack = self.IsBlessed;
            dmg.furyAttack = self.HasFury;
            best.TakeBasicDamage(dmg, self,
                PvpDetector.AttackDir(_inst, best),
                _inst.ConnectedUserAction, _inst.ImpactEffects);

            _hitCounts[best.ActorID] = minHits + 1;

            if (shadowCount + 1 >= _inst.numberOfAttacks) { _hitCounts.Clear(); }

            PvpDetector.ToggleHasHit(_inst);
            return (true, true);
        }

        internal void Reset() { _hitCounts.Clear(); }
    }
}
