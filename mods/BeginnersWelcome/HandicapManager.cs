using HarmonyLib;
using RR;
using RR.Game;
using RR.Game.Damage;
using RR.Game.Stats;
using System.Reflection;
using UnityEngine;

namespace BeginnersWelcome {
    public static class HandicapManager {
        private static FieldInfo _healthStatsField;

        public static bool Init() {
            _healthStatsField = AccessTools.Field(typeof(Health), "_stats");
            if (_healthStatsField == null) {
                BeginnersWelcomeMod.PublicLogger.LogWarning("BeginnersWelcome: Could not find Health._stats — damage patches inactive.");
                return false;
            }
            return true;
        }

        public static bool OnTakeBasicDamage(Health health, ref DamageDescriptor dmgDesc) {
            var player = FindPlayerForHealth(health);
            if (player == null) {
                return true;
            }


            float multiplier = HandicapState.Multiplier(player.SlotIndex);
            if (multiplier == 1f) {
                return true;
            }


            dmgDesc.damageValue /= multiplier;
            return true;
        }

        public static bool OnTakeDOTDamage(Health health, ref float damage) {
            var player = FindPlayerForHealth(health);
            if (player == null) {
                return true;
            }


            float multiplier = HandicapState.Multiplier(player.SlotIndex);
            if (multiplier == 1f) {
                return true;
            }


            damage /= multiplier;
            return true;
        }

        private static Player FindPlayerForHealth(Health health) {
            var stats = _healthStatsField?.GetValue(health) as StatsManager;
            if (stats == null || !stats.IsChampion) {
                return null;
            }


            var players = PlayerManager.Instance?.GetPlayers();
            if (players == null) {
                return null;
            }


            foreach (var p in players) {
                var champion = p.PlayableChampion as MonoBehaviour;
                if (champion != null && champion.GetComponent<Health>() == health) {
                    return p;
                }

            }
            return null;
        }
    }
}
