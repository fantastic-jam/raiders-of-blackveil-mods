using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModManager.Patch;
using ModRegistry;

namespace ModManager {
    [BepInPlugin(Id, Name, Version)]
    public class ModManagerMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.modmanager";
        public const string Name = "ModManager";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Utility);
        public string GetModName() => Name;
        public bool Disabled { get; private set; }
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            Disabled = true;
            _harmony?.UnpatchSelf();
        }

        public void Awake() {
            PublicLogger = Logger;
            try {
                _harmony = new Harmony(Id);
                ModManagerPatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
