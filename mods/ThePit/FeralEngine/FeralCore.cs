using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game.Stats;
using ThePit.FeralEngine.Abilities;
using UnityEngine;

namespace ThePit.FeralEngine {
    // Self-contained PvP subsystem for ThePit. Manages the combat patch lifecycle,
    // team assignments, and respawn invincibility state.
    //
    // Usage:
    //   FeralCore.Activate()   — on arena entry (applies PvP patches)
    //   FeralCore.Deactivate() — on match end / lobby return (removes PvP patches)
    public static class FeralCore {
        private static Harmony _feralCoreHarmony;

        // Team system — ConditionalWeakTable lets GC clean up dead actors automatically.
        private static readonly ConditionalWeakTable<StatsManager, TeamEntry> _teams = new();

        // Tracks actors that have been assigned a team so they can be explicitly removed on deactivate.
        private static readonly List<StatsManager> _assignedActors = new();

        // Respawn invincibility — actorId → absolute Time.time deadline.
        private static readonly Dictionary<int, float> _invincibleUntil = new();

        // True between Activate() and Deactivate().
        public static bool IsActive { get; private set; }

        // Apply PvP patches and open a session.
        // teams: null = FFA (everyone is an enemy of everyone).
        //        N    = team-based; N teams indexed 0..N-1; assign actors after calling this.
        public static void Activate(int? teams = null) {
            if (IsActive) {
                ThePitMod.PublicLogger.LogWarning("FeralCore.Activate called while already active — no-op.");
                return;
            }
            _feralCoreHarmony = new Harmony(ThePitMod.Id + ".feralcore");
            IsActive = true;
            FeralCorePatches.Apply(_feralCoreHarmony);
            ThePitMod.PublicLogger.LogInfo("FeralCore: activated.");
        }

        // Remove PvP patches and clear all session state including team assignments.
        public static void Deactivate() {
            if (!IsActive) { return; }
            IsActive = false;
            AbilityPatch.ResetAll();
            _feralCoreHarmony?.UnpatchSelf();
            _feralCoreHarmony = null;
            _invincibleUntil.Clear();
            foreach (var actor in _assignedActors) { _teams.Remove(actor); }
            _assignedActors.Clear();
            ThePitMod.PublicLogger.LogInfo("FeralCore: deactivated.");
        }

        // ── Team assignment ──────────────────────────────────────────────────────

        // Only meaningful when teams != null was passed to Activate.
        // Variant calls this after Activate to place champions and monsters into teams.
        public static void AssignTeam(StatsManager stats, int teamIndex) {
            if (!_assignedActors.Contains(stats)) { _assignedActors.Add(stats); }
            _teams.Remove(stats);
            _teams.Add(stats, new TeamEntry { Index = teamIndex });
        }

        // Returns the team index, or null if unassigned (treated as enemy of all in team mode).
        public static int? GetTeam(StatsManager stats) {
            return _teams.TryGetValue(stats, out var entry) ? (int?)entry.Index : null;
        }

        // ── Respawn invincibility ────────────────────────────────────────────────

        // Called by the variant's respawn logic. Stores an absolute deadline.
        public static void GrantRespawnInvincibility(int actorId, float duration) {
            _invincibleUntil[actorId] = Time.time + duration;
        }

        public static bool IsRespawnInvincible(int actorId) {
            return _invincibleUntil.TryGetValue(actorId, out float until) && Time.time < until;
        }

        // Checks the stored deadline without removing. Used by ClearInvincibilityCoroutine
        // to detect whether a newer invincibility was granted before clearing.
        internal static bool TryGetInvincibilityDeadline(int actorId, out float deadline) {
            return _invincibleUntil.TryGetValue(actorId, out deadline);
        }

        // Removes the invincibility record. Called by MatchController's clear coroutine
        // after the engine-level AllDamageDisabled flag is also cleared.
        internal static void RemoveInvincibility(int actorId) {
            _invincibleUntil.Remove(actorId);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private sealed class TeamEntry {
            internal int Index;
        }
    }
}
