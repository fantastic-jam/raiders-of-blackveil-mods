using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PlayerNameFix.Patch;

namespace PlayerNameFix {
    [BepInPlugin(Id, Name, Version)]
    public class PlayerNameFixMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.playernamefix";
        public const string Name = "PlayerNameFix";
        public const string Version = "0.0.1";
        public const string Author = "Laymain";

        public static ManualLogSource PublicLogger;

        private void Awake() {
            PublicLogger = Logger;

            try {
                PlayerNameFixPatch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
