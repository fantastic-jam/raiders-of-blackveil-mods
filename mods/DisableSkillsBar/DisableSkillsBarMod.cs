using System;
using BepInEx;
using BepInEx.Logging;
using DisableSkillsBar.Patch;
using HarmonyLib;
using ModRegistry;

namespace DisableSkillsBar {
    [BepInPlugin(Id, Name, Version)]
    public class DisableSkillsBarMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.disableskillsbar";
        public const string Name = "DisableSkillsBar";
        public const string Version = "0.4.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Cosmetics);
        public string GetModName() => Name;
        public bool Disabled => DisableSkillsBarPatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            DisableSkillsBarPatch.SetDisabled();
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
