using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using PerfectDodge.Patch;

namespace PerfectDodge {
    [BepInPlugin(Id, Name, Version)]
    public class PerfectDodgeMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.perfectdodge";
        public const string Name = "PerfectDodge";
        public const string Version = "0.4.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        /// <summary>
        /// Duration in seconds for the perfect-dodge timing window after dash press.
        /// </summary>
        public static ConfigEntry<float> PerfectDodgeWindowSeconds;

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
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
                PerfectDodgePatch.Apply(_harmony);
                PublicLogger.LogInfo(
                    $"{Name} by {Author} (version {Version}) loaded. " +
                    $"PerfectDodgeWindow={PerfectDodgeWindowSeconds.Value}s, DamageReduction=100%.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
