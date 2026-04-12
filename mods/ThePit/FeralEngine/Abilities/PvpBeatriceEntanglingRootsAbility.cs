using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpBeatriceEntanglingRootsAbility {
        internal static FieldInfo CasterField;

        internal static void Init() {
            CasterField = AccessTools.Field(typeof(BeatriceEntanglingRootAbility), "_projectileCaster");
            if (CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility._projectileCaster not found — Entangling Roots PvP inactive.");
            }
        }

        // Returns true to allow vanilla ApplyRoot, false to block it.
        // WitheredSeed hits are always allowed through (the seed revive logic must not be skipped).
        // Blocks self-hits and hits on invincible champions in PvP mode.
        // Also blocks when AllDamageDisabled is true (covers the one-frame gap before grace
        // invincibility where the caster is already expanded but FeralCore hasn't registered the
        // respawn invincibility yet).
        internal static bool ShouldApplyRoot(BeatriceEntanglingRootAbility inst, Collider targetCol) {
            if (targetCol.CompareTag("WitheredSeed")) { return true; }
            if (!targetCol.TryGetComponent<StatsManager>(out var stats) || !stats.IsChampion) { return true; }
            if (!ThePitState.IsDraftMode) { return false; }
            var caster = inst.Stats;
            if (caster != null && stats.ActorID == caster.ActorID) { return false; }
            if (FeralCore.IsRespawnInvincible(stats.ActorID)) { return false; }
            if (caster != null && FeralCore.IsRespawnInvincible(caster.ActorID)) { return false; }
            if (stats.Health != null && stats.Health.AllDamageDisabled) { return false; }
            return true;
        }

        private readonly BeatriceEntanglingRootAbility _inst;
        private bool _expanded;
        private ProjectileCaster _caster;
        private ProjectileCasterExpander.SavedMasks _saved;

        internal PvpBeatriceEntanglingRootsAbility(BeatriceEntanglingRootAbility inst) {
            _inst = inst;
            if (ThePitState.IsDraftMode && ThePitState.ArenaEntered) { Expand(); }
        }

        internal void Expand() {
            if (_expanded || !ProjectileCasterExpander.IsReady) { return; }
            _caster = CasterField?.GetValue(_inst) as ProjectileCaster;
            if (_caster != null) { _saved = ProjectileCasterExpander.Expand(_caster); }
            _expanded = true;
        }

        internal void Reset() {
            if (!_expanded) { return; }
            ProjectileCasterExpander.Reset(_caster, _saved);
            _expanded = false;
        }
    }
}
