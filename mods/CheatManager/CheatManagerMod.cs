using System;
using BepInEx;
using BepInEx.Logging;
using CheatManager.Patch;
using HarmonyLib;
using ModRegistry;

namespace CheatManager {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class CheatManagerMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.cheatmanager";
        public const string Name = "CheatManager";
        public const string Version = "0.3.1";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Cheat);
        public string GetModName() => Name;
        public string GetModDescription() => "Enables the built-in developer cheat hotkeys. Hold Alt in-game to see the reference overlay.";
        public bool IsClientRequired => false;
        public bool Disabled => CheatManagerPatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            CheatManagerPatch.SetDisabled();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            CheatManagerPatch.SetEnabled();
        }

        public void Awake() {
            PublicLogger = Logger;
            try {
                _harmony = new Harmony(Id);
                CheatManagerPatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
