using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HandyPurse.Patch;
using HarmonyLib;
using ModRegistry;

namespace HandyPurse {
    [BepInPlugin(Id, Name, Version)]
    public class HandyPurseMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.handypurse";
        public const string Name = "HandyPurse";
        public const string Version = "0.2.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private static ConfigEntry<int> _scrapCap;
        private static ConfigEntry<int> _blackCoinCap;
        private static ConfigEntry<int> _crystalCap;

        public static int ScrapCap => Math.Max(1, _scrapCap?.Value ?? 9999);
        public static int BlackCoinCap => Math.Max(1, _blackCoinCap?.Value ?? 999);
        public static int CrystalCap => Math.Max(1, _crystalCap?.Value ?? 999);

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public bool Disabled => HandyPursePatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            HandyPursePatch.SetDisabled();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            HandyPursePatch.SetEnabled();
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                BindConfig();
                _harmony = new Harmony(Id);
                HandyPursePatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
                PublicLogger.LogInfo($"Caps: Scrap={ScrapCap}, BlackCoin={BlackCoinCap}, Crystal={CrystalCap}");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }

        private void BindConfig() {
            _scrapCap = Config.Bind("Limits", "ScrapCap", 9999, "Max stack for Scrap.");
            _blackCoinCap = Config.Bind("Limits", "BlackCoinCap", 999, "Max stack for Black Coin.");
            _crystalCap = Config.Bind("Limits", "CrystalCap", 999, "Max stack for Crystals (BlackBlood and Glitter).");
        }
    }
}
