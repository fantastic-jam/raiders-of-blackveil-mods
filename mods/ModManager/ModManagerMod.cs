using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModManager.Patch;
using ModRegistry;

namespace ModManager {
    [BepInPlugin(Id, Name, Version)]
    public class ModManagerMod : BaseUnityPlugin, IModRegistrant {
        internal const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.modmanager";
        public const string Name = "ModManager";
        public const string Version = "0.3.0";
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
                ModManagerConfig.Init(Config);
                ModManagerPatch.Apply(_harmony);
                MenuStartPagePatch.Apply(_harmony);
                MenuPausePagePatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
                PublicLogger.LogWarning(
                    $"{Name} is deprecated and will be removed in a future update. " +
                    "Please migrate to WMF (Wildguard Mod Framework): https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
