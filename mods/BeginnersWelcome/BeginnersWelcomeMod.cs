using System;
using BepInEx;
using BepInEx.Logging;
using BeginnersWelcome.Patch;
using HarmonyLib;
using ModRegistry;

namespace BeginnersWelcome {
    [BepInPlugin(Id, Name, Version)]
    public class BeginnersWelcomeMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.beginnerswelcome";
        public const string Name = "BeginnersWelcome";
        public const string Version = "0.2.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public bool Disabled => BeginnersWelcomePatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            BeginnersWelcomePatch.SetDisabled();
        }

        public void Awake() {
            PublicLogger = Logger;
            try {
                _harmony = new Harmony(Id);
                BeginnersWelcomePatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
