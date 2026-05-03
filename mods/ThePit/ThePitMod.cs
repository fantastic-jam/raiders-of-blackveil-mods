using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using ThePit.Patch;
using WildguardModFramework;
using WildguardModFramework.Translation;

namespace ThePit {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class ThePitMod : BaseUnityPlugin, IModRegistrant, IGameModeProvider {
        public const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.thepit";
        public const string Name = "ThePit";
        public const string Version = "0.3.0";
        public const string Author = "christphe";
        private const string TargetGameVersion = "0.1.0_WIN_2026-01-29_180103_202c53513d";

        public static ManualLogSource PublicLogger;
        internal static T t;

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


        public string GetModType() => _gameVersionSupported ? nameof(ModType.GameMode) : nameof(ModType.Mod);
        public string GetModName() => Name;
        public string GetModDescription() => t("mod.description");
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

        public IReadOnlyList<GameModeVariant> GameModeVariants => new List<GameModeVariant> {
            new GameModeVariant(
                ThePitState.VariantDraft,
                t("variant.pvp.name"),
                t("variant.pvp.description"),
                joinMessage: null,
                runStartMessage: t("variant.pvp.run_start")
            ),
            // new GameModeVariant( // not yet exposed
            //     ThePitState.VariantMoba,
            //     t("variant.moba.name"),
            //     t("variant.moba.description"),
            //     joinMessage: null,
            //     runStartMessage: t("variant.moba.run_start")
            // ),
        };

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
        private bool _gameVersionSupported = true;
#if DEV_HOTRELOAD
        private Harmony _devHarmony;
#endif

        private void CheckGameVersion() {
            try {
                var versionFile = Path.Combine(Paths.GameRootPath, "version.txt");
                if (!File.Exists(versionFile)) {
                    return;
                }

                var gameVersion = File.ReadAllText(versionFile).Trim();
                if (gameVersion == TargetGameVersion) {
                    return;
                }

                _gameVersionSupported = false;
                PublicLogger.LogWarning($"{Name} v{Version}: unsupported game version '{gameVersion}' (expected '{TargetGameVersion}'). Game mode not available.");
            }
            catch {
                // can't read version file — let patch reflection checks catch real breakage
            }
        }

        public void Awake() {
            PublicLogger = Logger;
            t = TranslationService.For(Name, Info.Location);
            CheckGameVersion();

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
