using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OfflineMode.Patch;

namespace OfflineMode {
    [BepInPlugin(Id, Name, Version)]
    public class OfflineModeMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.offlinemode";
        public const string Name = "OfflineMode";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        public string GetModType() => "Utility";
        public string GetModName() => Name;
        public string GetModDescription() => "Play offline and sync your save when you reconnect.";
        public void Disable() { }

        private void Awake() {
            PublicLogger = Logger;

            try {
                OfflineModePatch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
