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
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        // ── Progression config ───────────────────────────────────────────────────
        // Original: PerkInterval = 30 s, XpTickInterval = 45 s, MatchDuration = 600 s.
        // Halving intervals makes progression approximately 2× faster.
        public static ConfigEntry<float> CfgPerkIntervalSeconds;
        public static ConfigEntry<float> CfgXpTickIntervalSeconds;
        public static ConfigEntry<float> CfgMatchDurationSeconds;
        public static ConfigEntry<int> CfgMaxPerksPerPlayer;

        // ── Stepper option lists ──────────────────────────────────────────────────
        // Each is a comma-separated list of "Label:Value" pairs used to populate the
        // host config overlay steppers. Parse errors fall back to the built-in defaults.
        public static ConfigEntry<string> CfgDurationOptions;
        public static ConfigEntry<string> CfgDropRateOptions;
        public static ConfigEntry<string> CfgInitialPerksOptions;
        public static ConfigEntry<string> CfgDamageReductionOptions;

#if DEV_HOTRELOAD
        public static ConfigEntry<string> CfgDevHotReloadDllPath;
#endif


        public string GetModType() => nameof(ModType.GameMode);
        public string GetModName() => Name;
        public string GetModDescription() => "A proving ground for raiders and newcomers. Test your mettle in brutal free-for-all brawls.";
        public bool IsClientRequired => false;
        public bool Disabled => !ThePitState.IsActive;

        public void Enable() {
            ThePitState.IsActive = true;
#if DEV_HOTRELOAD
            Dev.HotReloadController.Enable();
#endif
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
#if DEV_HOTRELOAD
            Dev.HotReloadController.Disable();
#endif
            PublicLogger.LogInfo($"{Name}: disabled.");
        }

        private static readonly List<GameModeVariant> _variants = new() {
            new GameModeVariant(
                ThePitState.VariantDraft,
                "The Pit — PvP",
                "Start bare. Earn perks over time. Last one standing wins.",
                joinMessage: null,
                runStartMessage: "The Pit is open. Survive."
            ),
            // new GameModeVariant( // not yet exposed
            //     ThePitState.VariantMoba,
            //     "The Pit — Protect the Core",
            //     "Defend your Core. Destroy the others. Minions march, coins flow.",
            //     joinMessage: null,
            //     runStartMessage: "Defend the Core. Destroy all others."
            // ),
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
#if DEV_HOTRELOAD
        private Harmony _devHarmony;
#endif

        private void Awake() {
            PublicLogger = Logger;

            CfgPerkIntervalSeconds = Config.Bind(
                "Rebalancing", "PerkIntervalSeconds", 30f,
                "Base seconds between perk drops. The Drop Speed stepper multiplies this.");
            CfgXpTickIntervalSeconds = Config.Bind(
                "Rebalancing", "XpTickIntervalSeconds", 45f,
                "Base seconds per XP level-up. The Drop Speed stepper multiplies this. XP stops at level 20.");
            CfgMatchDurationSeconds = Config.Bind(
                "Rebalancing", "MatchDurationSeconds", 300f,
                "Fallback match duration in seconds when no overlay choice is saved.");
            CfgMaxPerksPerPlayer = Config.Bind(
                "Rebalancing", "MaxPerksPerPlayer", 30,
                "Maximum perks a player can hold (chest rounds and drip combined).");

            CfgDurationOptions = Config.Bind(
                "Rebalancing", "DurationOptions",
                "2 min:120,5 min:300,10 min:600,15 min:900,20 min:1200",
                "Match duration stepper options. Format: Label:seconds,... — parse errors revert to defaults.");
            CfgDropRateOptions = Config.Bind(
                "Rebalancing", "DropRateOptions",
                "Sluggish:3.0,Slow:2.0,Normal:1.0,Fast:0.67,Rapid:0.5,Frenzy:0.33",
                "Drop speed stepper options. Format: Label:intervalMultiplier,... (1.0 = base rate, 2.0 = half rate).");
            CfgInitialPerksOptions = Config.Bind(
                "Rebalancing", "InitialPerksOptions",
                "0,1,2,3,4,5,6,7,8,9,10,11,12",
                "Initial perk chest round count options. Format: comma-separated integers, or Label:int pairs.");
            CfgDamageReductionOptions = Config.Bind(
                "Rebalancing", "DamageReductionOptions",
                "Off:1,Gentle:5,Medium:10,Strong:20,Extreme:40",
                "Damage reduction stepper options. Format: Label:maxFactor,... — at max XP level, incoming damage is divided by maxFactor. Off=1 means no reduction.");

            try {
                _draftHarmony = new Harmony(Id + ".draft");
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }

#if DEV_HOTRELOAD
            CfgDevHotReloadDllPath = Config.Bind(
                "DevHotReload", "DllPath", "",
                "Absolute path to the Debug build output DLL for F9 hot-reload. Example: C:/projects/.../mods/ThePit/bin/Debug/ThePit.dll");
            _devHarmony = new Harmony(Id + ".dev");
            Dev.HotReloadController.Initialize(_devHarmony, CfgDevHotReloadDllPath.Value);
            PublicLogger.LogWarning($"[HotReload] DEV BUILD. DLL: {CfgDevHotReloadDllPath.Value}");
#endif
        }

    }
}
