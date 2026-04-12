using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Enemies;
using RR.Game.Stats;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpChampionMinion {
        internal static FieldInfo TargetCharacterField { get; private set; }

        internal static void Init() {
            TargetCharacterField =
                AccessTools.Field(typeof(NetworkChampionMinion).BaseType, "_targetCharacter") ??
                AccessTools.Field(typeof(NetworkChampionMinion), "_targetCharacter");
            if (TargetCharacterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionMinion._targetCharacter not found — minion champion targeting inactive.");
            }
        }

        private readonly NetworkChampionMinion _inst;

        internal PvpChampionMinion(NetworkChampionMinion inst) { _inst = inst; }

        internal bool SelectEnemyTarget() {
            if (!_inst.Object.HasStateAuthority) { return false; }

            var ownerStats = _inst.MinionOwner?.Stats;
            if (ownerStats == null) { return false; }

            float closestDist = _inst.AttackDistance;
            StatsManager closestChamp = null;

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (champ.Stats.ActorID == ownerStats.ActorID) { continue; }
                if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

                float dist = Vector3.Distance(champ.transform.position, _inst.MinionOwner.transform.position);
                if (dist < closestDist) { closestDist = dist; closestChamp = champ.Stats; }
            }

            if (closestChamp == null) { return false; }

            TargetCharacterField?.SetValue(_inst, closestChamp.Character);
            return true;
        }

        // OnDead casts _targetCharacter to NetworkEnemyBase and passes it to RemoveMeAsTargetter.
        // When we set _targetCharacter to a champion, the cast returns null and RemoveMeAsTargetter
        // crashes on enemy.Stats. Clear the field before OnDead runs if it holds a non-enemy target.
        internal void ClearNonEnemyTarget() {
            var target = TargetCharacterField?.GetValue(_inst);
            if (target != null && target is not NetworkEnemyBase) {
                TargetCharacterField.SetValue(_inst, null);
            }
        }

        internal bool GeneralAttackConditions() {
            var ownerStats = _inst.MinionOwner?.Stats;
            if (ownerStats == null) { return false; }

            foreach (var player in PlayerManager.Instance.GetPlayers()) {
                var champ = player.PlayableChampion;
                if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                if (champ.Stats.ActorID == ownerStats.ActorID) { continue; }

                float dist = Vector3.Distance(champ.transform.position, _inst.MinionOwner.transform.position);
                if (dist <= _inst.AttackDistance) { return true; }
            }

            return false;
        }
    }
}
