using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using CheatManager.Patch;

namespace CheatManager {
    [BepInPlugin(Id, Name, Version)]
    public class CheatManagerMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.cheatmanager";
        public const string Name = "CheatManager";
        public const string Version = "0.0.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private void Awake() {
            PublicLogger = Logger;

            try {
                CheatManagerPatch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
