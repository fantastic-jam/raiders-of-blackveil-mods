using System.Collections.Generic;

namespace ThePit {
    internal static class ThePitState {
        internal const string VariantDraft = "beta";
        internal const string VariantMoba = "moba";

        // Full compound IDs as stored by WMF: "{pluginGuid}::{variantId}".
        // We own both parts, so these are constants, not runtime string ops.
        internal const string WmfVariantIdBeta = ThePitMod.Id + "::" + VariantDraft;
        internal const string WmfVariantIdMoba = ThePitMod.Id + "::" + VariantMoba;

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

        // Set true once the arena grace period expires — enables the HUD combat timer.
        internal static bool CombatStarted { get; set; }

        // Set true during the initial perk chest phase — blocks door activation until all
        // chest rounds complete and PerkDripController opens the door manually.
        internal static bool ChestPhaseActive { get; set; }

        // Set by the host config overlay before the session starts.
        // 0 means "fall back to the BepInEx config value".
        internal static float MatchDurationSecondsOverride { get; set; }

        // Scales perk and XP drop intervals. 1.0 = normal, 2.0 = half rate, 0.5 = double rate.
        internal static float DropIntervalMultiplier { get; set; } = 1.0f;

        // Number of initial perk chest rounds before the door opens. -1 = use PerkDripController's default. 0 = skip chest phase.
        internal static int InitialChestRoundsOverride { get; set; } = -1;

        // At max XP level, incoming champion damage is divided by this factor.
        // 1 = no reduction. 0 = use the BepInEx cfg default option.
        internal static float DamageReductionMaxFactor { get; set; }

        // ActorID → kill count for the current match.
        internal static Dictionary<int, int> KillCounts { get; } = new();

        // Pre-resolved at arena entry by MatchController.StartArena(). Avoids config-string
        // parsing on every damage event. Reset to 0 by ResetMatchState().
        internal static float CachedDamageReductionFactor { get; set; }

        // Resolved damage reduction factor: uses the overlay override if set, otherwise parses
        // the default from the cfg option list (index 3 = "Strong:20" in the default list).
        internal static float ResolvedDamageReductionFactor {
            get {
                if (DamageReductionMaxFactor > 0f) { return DamageReductionMaxFactor; }
                var raw = ThePitMod.CfgDamageReductionOptions?.Value;
                if (string.IsNullOrEmpty(raw)) { return 20f; }
                try {
                    var entries = raw.Split(',');
                    int idx = System.Math.Min(3, entries.Length - 1);
                    int colon = entries[idx].IndexOf(':');
                    if (colon < 0) { return 20f; }
                    return float.Parse(entries[idx][(colon + 1)..].Trim(),
                        System.Globalization.CultureInfo.InvariantCulture);
                }
                catch { return 20f; }
            }
        }

        internal static void ResetMatchState() {
            MatchStarted = false;
            MiniBossRedirected = false;
            ArenaEntered = false;
            MatchEnded = false;
            ChestPhaseActive = false;
            CombatStarted = false;
            CachedDamageReductionFactor = 0f;
            KillCounts.Clear();
        }
    }
}
