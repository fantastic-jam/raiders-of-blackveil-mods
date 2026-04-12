using System.Collections.Generic;
using System.Reflection;
using Fusion;
using HarmonyLib;
using RR;
using RR.Game.Character;
using RR.Game.Perk;
using RR.Game.Stats;
using ThePit.FeralEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpBlazeSpecialArea {
        private static FieldInfo _alliesInsideField;
        private static FieldInfo _tempStatsListField;

        private readonly BlazeSpecialArea _inst;
        private bool _burnTimerSet;
        private TickTimer _burnTimer;

        internal static void Init() {
            _alliesInsideField = AccessTools.Field(typeof(BlazeSpecialArea), "AlliesInside");
            if (_alliesInsideField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea.AlliesInside not found — Heat Aura ally buff fix inactive.");
            }

            _tempStatsListField = AccessTools.Field(typeof(BlazeSpecialArea), "_tempStatsList");
            if (_tempStatsListField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeSpecialArea._tempStatsList not found — Heat Aura ally buff fix inactive.");
            }
        }

        internal PvpBlazeSpecialArea(BlazeSpecialArea inst) { _inst = inst; }

        // Returns false to block the original (PvP mode handled), true to run original (fallback).
        internal bool UpdateAuraEffect(bool checkWhoIsInside) {
            if (_alliesInsideField == null || _tempStatsListField == null) { return true; }

            var casterStats = _inst.areaCaster?.Stats;
            if (casterStats == null) { return true; }

            var alliesInside = _alliesInsideField.GetValue(_inst) as List<StatsManager>;
            var tempStats = _tempStatsListField.GetValue(_inst) as List<StatsManager>;
            if (alliesInside == null || tempStats == null) { return true; }

            tempStats.Clear();
            if (checkWhoIsInside && casterStats.IsAlive) {
                var diff = casterStats.transform.position - _inst.transform.position;
                diff.y = 0f;
                if (diff.magnitude <= _inst.ActRadius) { tempStats.Add(casterStats); }
            }

            foreach (var ally in alliesInside) {
                if (!tempStats.Contains(ally)) {
                    ally.ClearTemporaryModifiedProperty(
                        Property.CriticalStrikeChance, _inst.criticalChanceIncrementPCT);
                }
            }
            foreach (var sm in tempStats) {
                if (!alliesInside.Contains(sm)) {
                    sm.ModifyPropertyForFrames(
                        Property.CriticalStrikeChance, _inst.criticalChanceIncrementPCT, 999999);
                }
            }
            alliesInside.Clear();
            alliesInside.AddRange(tempStats);
            return false;
        }

        internal void OnFixedUpdate() {
            if (!_inst.Object.HasStateAuthority) { return; }

            var casterStats = _inst.areaCaster?.Stats;
            if (casterStats == null) { return; }

            if (!_burnTimerSet || _burnTimer.Expired(_inst.Runner)) {
                _burnTimer = TickTimer.CreateFromSeconds(_inst.Runner, _inst.burnEffectRepeatTime);
                _burnTimerSet = true;

                foreach (var player in PlayerManager.Instance.GetPlayers()) {
                    var champ = player.PlayableChampion;
                    if (champ?.Stats == null || !champ.Stats.IsAlive) { continue; }
                    if (champ.Stats.ActorID == casterStats.ActorID) { continue; }
                    if (FeralCore.IsRespawnInvincible(champ.Stats.ActorID)) { continue; }

                    var diff = champ.transform.position - _inst.transform.position;
                    diff.y = 0f;
                    if (diff.magnitude > _inst.ActRadius) { continue; }

                    champ.Stats.Burn?.AddBurn(casterStats);
                }
            }
        }

        internal void Reset() {
            _burnTimerSet = false;
        }
    }
}
