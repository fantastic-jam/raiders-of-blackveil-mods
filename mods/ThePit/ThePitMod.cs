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
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.thepit";
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
            PublicLogger.LogInfo($"{Name}: variant '{variantId}' activated.");
        }

        private Harmony _harmony;

        private void Awake() {
            PublicLogger = Logger;

            CfgPerkIntervalSeconds = Config.Bind(
                "Progression", "PerkIntervalSeconds", 30f,
                "Seconds between perk drops in the arena. Default: 30s.");
            CfgXpTickIntervalSeconds = Config.Bind(
                "Progression", "XpTickIntervalSeconds", 45f,
                "Seconds between XP level-up ticks in the arena. Default: 45s.");
            CfgMatchDurationSeconds = Config.Bind(
                "Progression", "MatchDurationSeconds", 600f,
                "Total match duration in seconds. Default: 600s (10 minutes).");

            try {
                _harmony = new Harmony(Id);
                if (!ThePitPatch.Apply(_harmony)) {
                    _harmony.UnpatchSelf();
                    ThePitState.IsActive = false;
                    PublicLogger.LogError("============================================================");
                    PublicLogger.LogError($"{Name} v{Version}: game assembly breaking change detected.");
                    PublicLogger.LogError("Mod disabled. Update the mod or report a bug (include log).");
                    PublicLogger.LogError("============================================================");
                    return;
                }
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
