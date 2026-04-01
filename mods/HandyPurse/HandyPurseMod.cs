using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using HandyPurse.Patch;

namespace HandyPurse
{
    [BepInPlugin(Id, Name, Version)]
    public class HandyPurseMod : BaseUnityPlugin
    {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.handypurse";
        public const string Name = "HandyPurse";
        public const string Version = "0.0.2";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private static ConfigEntry<int> _scrapCap;
        private static ConfigEntry<int> _blackCoinCap;
        private static ConfigEntry<int> _crystalCap;

        public static int ScrapCap => Math.Max(1, _scrapCap?.Value ?? 9999);
        public static int BlackCoinCap => Math.Max(1, _blackCoinCap?.Value ?? 999);
        public static int CrystalCap => Math.Max(1, _crystalCap?.Value ?? 999);

        private void Awake()
        {
            PublicLogger = Logger;
            try
            {
                BindConfig();
                HandyPursePatch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
                PublicLogger.LogInfo($"Caps: Scrap={ScrapCap}, BlackCoin={BlackCoinCap}, Crystal={CrystalCap}");
            }
            catch (Exception ex)
            {
                PublicLogger.LogError(ex);
            }
        }

        private void BindConfig()
        {
            _scrapCap = Config.Bind("Limits", "ScrapCap", 9999, "Max stack for Scrap.");
            _blackCoinCap = Config.Bind("Limits", "BlackCoinCap", 999, "Max stack for Black Coin.");
            _crystalCap = Config.Bind("Limits", "CrystalCap", 999, "Max stack for Crystals (BlackBlood and Glitter).");
        }
    }
}


