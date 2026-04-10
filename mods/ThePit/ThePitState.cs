using System.Collections.Generic;
using ThePit.Patch.Abilities;
using UnityEngine;

namespace ThePit {
    internal static class ThePitState {
        internal const string VariantDraft = "beta";
        internal const string VariantMoba = "moba";

        internal static bool IsActive { get; set; }
        internal static string ActiveVariant { get; set; }

        internal static bool IsDraftMode => IsActive && ActiveVariant == VariantDraft;
        internal static bool IsMobaMode => IsActive && ActiveVariant == VariantMoba;

        // Set true after the first EventBeginLevel of a match — prevents re-init on room transitions.
        internal static bool MatchStarted { get; set; }

        // Set true after the first door transition is redirected to MiniBoss.
        internal static bool MiniBossRedirected { get; set; }

        // Set true when EventBeginLevel fires for the SlashBash room — starts grace period + match timer.
        internal static bool ArenaEntered { get; set; }

        // Set true when the match timer fires — suppresses respawn on subsequent deaths.
        internal static bool MatchEnded { get; set; }

        // Set true during the initial perk chest phase — blocks door activation until all
        // chest rounds complete and PerkDripController opens the door manually.
        internal static bool ChestPhaseActive { get; set; }

        // True while PvP combat should be active: mod on, in the arena, timer still running.
        internal static bool IsAttackPossible => IsDraftMode && ArenaEntered && !MatchEnded;

        // ActorID → Time.time deadline. Blocks all incoming damage until deadline passes.
        internal static Dictionary<int, float> InvincibleUntil { get; } = new();

        // ActorID → kill count for the current match.
        internal static Dictionary<int, int> KillCounts { get; } = new();

        internal static bool IsPlayerInvincible(int actorId) =>
            InvincibleUntil.TryGetValue(actorId, out float until) && Time.time < until;

        internal static void ResetMatchState() {
            MatchStarted = false;
            MiniBossRedirected = false;
            ArenaEntered = false;
            MatchEnded = false;
            ChestPhaseActive = false;
            InvincibleUntil.Clear();
            KillCounts.Clear();
            ShameleonShadowDancePatch.Reset();
            ShameleonTongueLeapPatch.Reset();
            BlazeBlastWavePatch.Reset();
            SunStrikeAreaPatch.Reset();
            BlazeAttackPatch.ResetAllCasters();
            BlazeSpecialAreaPatch.Reset();
            BeatriceAttackPatch.ResetAllCasters();
            BeatriceEntanglingRootsPatch.ResetAllCasters();
            BeatriceLotusFlowerPatch.ResetAllCasters();
            WitheredSeedBrainPatch.ResetAllCasters();
        }
    }
}
