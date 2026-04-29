using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using PerfectDodge.Patch;

namespace PerfectDodge {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class PerfectDodgeMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.perfectdodge";
        public const string Name = "PerfectDodge";
        public const string Version = "0.5.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        /// <summary>
        /// Duration in seconds for the perfect-dodge timing window after dash press.
        /// </summary>
        public static ConfigEntry<float> PerfectDodgeWindowSeconds;

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public string GetModDescription() => "Negates a hit if you get struck within a short window after pressing dash, and refunds the dash charge.";
        public bool IsClientRequired => false;
        public bool Disabled => PerfectDodgePatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            PerfectDodgePatch.SetDisabled();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            PerfectDodgePatch.SetEnabled();
        }

        private void Awake() {
            PublicLogger = Logger;

            PerfectDodgeWindowSeconds = Config.Bind(
                "PerfectDodge",
                "PerfectDodgeWindowSeconds",
                0.3f,
                new ConfigDescription(
                    "Perfect-dodge timing window in seconds after dash press.",
                    new AcceptableValueRange<float>(0.01f, 1f)));

            try {
                _harmony = new Harmony(Id);
                if (!PerfectDodgePatch.Apply(_harmony)) {
                    _harmony.UnpatchSelf();
                    PerfectDodgePatch.SetDisabled();
                    LogBreakingChange();
                    return;
                }
                PublicLogger.LogInfo(
                    $"{Name} by {Author} (version {Version}) loaded. " +
                    $"PerfectDodgeWindow={PerfectDodgeWindowSeconds.Value}s, DamageReduction=100%.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }

        private void LogBreakingChange() {
            PublicLogger.LogError("============================================================");
            PublicLogger.LogError($"{Name} v{Version}: game assembly breaking change detected.");
            PublicLogger.LogError($"Mod disabled. Update the mod or report a bug (include log).");
            PublicLogger.LogError("============================================================");
        }
    }
}
