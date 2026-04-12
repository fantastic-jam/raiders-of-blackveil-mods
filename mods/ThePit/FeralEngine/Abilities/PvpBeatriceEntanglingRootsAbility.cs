using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Per-instance state for Beatrice's Entangling Roots in PvP.
    // Owns: ProjectileCaster mask expansion, ApplyRoot gating, and rooted-victim tracking.
    //
    // Victim tracking: when a champion is successfully rooted, we store their StatsManager.
    // OnFixedUpdate clears their root immediately if they die — vanilla never does this,
    // so without it _rootedDamageTimer on the victim's Movement keeps firing into their next life.
    internal class PvpBeatriceEntanglingRootsAbility {
        internal static FieldInfo CasterField;

        internal static void Init() {
            CasterField = AccessTools.Field(typeof(BeatriceEntanglingRootAbility), "_projectileCaster");
            if (CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceEntanglingRootAbility._projectileCaster not found — Entangling Roots PvP inactive.");
            }
        }

        private readonly BeatriceEntanglingRootAbility _inst;
        private bool _expanded;
        private ProjectileCaster _caster;
        private ProjectileCasterExpander.SavedMasks _saved;

        // Prefix→postfix handshake: true when the prefix allowed vanilla ApplyRoot to run.
        private bool _rootAllowed;
        // The champion most recently rooted by this instance; null when not tracking.
        private StatsManager _rootedVictim;

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
            _rootedVictim = null;
        }

        // Returns true to allow vanilla ApplyRoot, false to block.
        // Sets _rootAllowed so the postfix knows whether vanilla ran.
        internal bool OnApplyRootPrefix(Collider targetCol) {
            _rootAllowed = false;

            // WitheredSeed hits must always go through vanilla (seed revive logic lives there).
            if (targetCol.CompareTag("WitheredSeed")) { _rootAllowed = true; return true; }

            // Non-champion colliders (walls, terrain etc.) are vanilla's responsibility.
            if (!targetCol.TryGetComponent<StatsManager>(out var stats) || !stats.IsChampion) {
                _rootAllowed = true;
                return true;
            }

            if (!ThePitState.IsDraftMode) { return false; }

            var caster = _inst.Stats;
            if (caster != null && stats.ActorID == caster.ActorID) { return false; }
            if (FeralCore.IsRespawnInvincible(stats.ActorID)) { return false; }
            if (caster != null && FeralCore.IsRespawnInvincible(caster.ActorID)) { return false; }
            if (stats.Health != null && stats.Health.AllDamageDisabled) { return false; }

            _rootAllowed = true;
            return true;
        }

        // Runs after vanilla ApplyRoot (even when blocked — guarded by _rootAllowed).
        // If the target is a champion that is now rooted, start tracking them.
        internal void OnApplyRootPostfix(Collider targetCol) {
            if (!_rootAllowed) { return; }
            if (!targetCol.TryGetComponent<StatsManager>(out var stats) || !stats.IsChampion) { return; }
            if (stats.IsRooted) { _rootedVictim = stats; }
        }

        // Called every FixedUpdateNetwork frame (server only).
        // Clears root immediately when the victim dies so _rootedDamageTimer on their
        // Movement component does not bleed into their next life.
        internal void OnFixedUpdate() {
            if (_inst.Object == null || !_inst.Object.HasStateAuthority) { return; }
            if (_rootedVictim == null) { return; }

            if (!_rootedVictim.IsAlive) {
                _rootedVictim.Movement?.ResetRooted();
                _rootedVictim = null;
            } else if (!_rootedVictim.IsRooted) {
                // Root expired naturally — no cleanup needed, stop tracking.
                _rootedVictim = null;
            }
        }
    }
}
