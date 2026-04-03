using System;
using BepInEx;
using BepInEx.Logging;
using BeginnersWelcome.Patch;
using HarmonyLib;

namespace BeginnersWelcome {
    [BepInPlugin(Id, Name, Version)]
    public class BeginnersWelcomeMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.beginnerswelcome";
        public const string Name = "BeginnersWelcome";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        public void Awake() {
            PublicLogger = Logger;
            try {
                BeginnersWelcomePatch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
