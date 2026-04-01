using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using DisableSkillsBar.Patch;

namespace DisableSkillsBar {
    [BepInPlugin(Id, Name, Version)]
    public class DisableSkillsBarMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.disableskillsbar";
        public const string Name = "DisableSkillsBar";
        public const string Version = "0.3.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private void Awake() {
            PublicLogger = Logger;
            try {
                DisableSkillsBarPatch.Apply(new Harmony(Id));
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}



