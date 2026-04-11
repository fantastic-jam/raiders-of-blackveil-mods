using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using ThePit.Patch;

namespace ThePit {
    [BepInPlugin(Id, Name, Version)]
    public class ThePitMod : BaseUnityPlugin, IModRegistrant, IGameModeProvider {
        public const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.thepit";
        public const string Name = "ThePit";
        public const string Version = "0.0.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        // ── Progression config ───────────────────────────────────────────────────
        // Original: PerkInterval = 30 s, XpTickInterval = 45 s, MatchDuration = 600 s.
        // Halving intervals makes progression approximately 2× faster.
        public static ConfigEntry<float> CfgPerkIntervalSeconds;
        public static ConfigEntry<float> CfgXpTickIntervalSeconds;
        public static ConfigEntry<float> CfgMatchDurationSeconds;

        // ── Stepper option lists ──────────────────────────────────────────────────
        // Each is a comma-separated list of "Label:Value" pairs used to populate the
        // host config overlay steppers. Parse errors fall back to the built-in defaults.
        public static ConfigEntry<string> CfgDurationOptions;
        public static ConfigEntry<string> CfgDropRateOptions;
        public static ConfigEntry<string> CfgInitialPerksOptions;
        public static ConfigEntry<string> CfgDamageReductionOptions;

        public string GetModType() => nameof(ModType.GameMode);
        public string GetModName() => Name;
        public string GetModDescription() => "A proving ground for raiders and newcomers. Test your mettle in brutal free-for-all brawls.";
        public bool IsClientRequired => false;
        public bool Disabled => !ThePitState.IsActive;

        public void Enable() {
            ThePitState.IsActive = true;
            PublicLogger.LogInfo($"{Name}: enabled.");
        }

        public void Disable() {
            ThePitState.IsActive = false;
            ThePitState.ActiveVariant = null;
            MatchController.Stop();
            if (_draftPatchesApplied) {
                _draftHarmony?.UnpatchSelf();
                _draftPatchesApplied = false;
            }
            PublicLogger.LogInfo($"{Name}: disabled.");
        }

        private static readonly List<GameModeVariant> _variants = new() {
            new GameModeVariant(
                ThePitState.VariantDraft,
                "The Pit — Beta",
                "Start bare. Earn perks over time. Last one standing wins.",
                joinMessage: null,
                runStartMessage: "The Pit is open. Survive."
            ),
            new GameModeVariant(
                ThePitState.VariantMoba,
                "The Pit — Protect the Core",
                "Defend your Core. Destroy the others. Minions march, coins flow.",
                joinMessage: null,
                runStartMessage: "Defend the Core. Destroy all others."
            ),
        };

        public IReadOnlyList<GameModeVariant> GameModeVariants => _variants;

        public void EnableVariant(string variantId) {
            ThePitState.ActiveVariant = variantId;
            if (variantId == ThePitState.VariantDraft && !_draftPatchesApplied) {
                if (!ThePitPatch.Apply(_draftHarmony)) {
                    _draftHarmony.UnpatchSelf();
                    ThePitState.IsActive = false;
                    PublicLogger.LogError("============================================================");
                    PublicLogger.LogError($"{Name} v{Version}: game assembly breaking change detected.");
                    PublicLogger.LogError("Mod disabled. Update the mod or report a bug (include log).");
                    PublicLogger.LogError("============================================================");
                } else {
                    _draftPatchesApplied = true;
                }
            }
            PublicLogger.LogInfo($"{Name}: variant '{variantId}' activated.");
        }

        private Harmony _draftHarmony;
        private bool _draftPatchesApplied;

        private void Awake() {
            PublicLogger = Logger;

            CfgPerkIntervalSeconds = Config.Bind(
                "Progression", "PerkIntervalSeconds", 30f,
                "Base seconds between perk drops. The Drop Rate stepper multiplies this.");
            CfgXpTickIntervalSeconds = Config.Bind(
                "Progression", "XpTickIntervalSeconds", 45f,
                "Base seconds per XP level-up. The Drop Rate stepper multiplies this.");
            CfgMatchDurationSeconds = Config.Bind(
                "Progression", "MatchDurationSeconds", 600f,
                "Fallback match duration in seconds when no overlay choice is saved.");

            CfgDurationOptions = Config.Bind(
                "Steppers", "DurationOptions",
                "5 min:300,8 min:480,10 min:600,15 min:900,20 min:1200",
                "Match duration stepper options. Format: Label:seconds,... — parse errors revert to defaults.");
            CfgDropRateOptions = Config.Bind(
                "Steppers", "DropRateOptions",
                "Trickle:3.0,Slow:2.0,Normal:1.0,Fast:0.67,Rapid:0.5,Frenzy:0.33",
                "Drop rate stepper options. Format: Label:intervalMultiplier,... (1.0 = base rate, 2.0 = half rate).");
            CfgInitialPerksOptions = Config.Bind(
                "Steppers", "InitialPerksOptions",
                "1,2,3,4,5,6,7,8,9,10,11,12",
                "Initial perk chest round count options. Format: comma-separated integers, or Label:int pairs.");
            CfgDamageReductionOptions = Config.Bind(
                "Steppers", "DamageReductionOptions",
                "Off:1,Gentle:5,Medium:10,Strong:20,Extreme:40",
                "Damage reduction stepper options. Format: Label:maxFactor,... — at max XP level, incoming damage is divided by maxFactor. Off=1 means no reduction.");

            try {
                _draftHarmony = new Harmony(Id + ".draft");
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
