using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    internal class PvpChampionMinion {
        internal static readonly FieldInfo TargetCharacterField =
            AccessTools.Field(typeof(NetworkChampionMinion).BaseType, "_targetCharacter") ??
            AccessTools.Field(typeof(NetworkChampionMinion), "_targetCharacter");

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
                if (ThePitState.IsPlayerInvincible(champ.Stats.ActorID)) { continue; }

                float dist = Vector3.Distance(champ.transform.position, _inst.MinionOwner.transform.position);
                if (dist < closestDist) { closestDist = dist; closestChamp = champ.Stats; }
            }

            if (closestChamp == null) { return false; }

            TargetCharacterField?.SetValue(_inst, closestChamp.Character);
            return true;
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
