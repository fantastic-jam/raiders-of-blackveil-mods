using System;
using BepInEx;
using BepInEx.Logging;
using DisableSkillsBar.Patch;
using HarmonyLib;

namespace DisableSkillsBar {
    [BepInPlugin(Id, Name, Version)]
    public class DisableSkillsBarMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.disableskillsbar";
        public const string Name = "DisableSkillsBar";
        public const string Version = "0.5.2";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private Harmony _harmony;

        public string GetModType() => "Cosmetics";
        public string GetModName() => Name;
        public string GetModDescription() => "Prevents the skills bar from reacting to accidental hover or clicks during combat. Hold Alt to interact normally.";
        public bool Disabled => DisableSkillsBarPatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            DisableSkillsBarPatch.SetDisabled();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            DisableSkillsBarPatch.SetEnabled();
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                _harmony = new Harmony(Id);
                DisableSkillsBarPatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
