using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using OfflineMode.Patch;
using WildguardModFramework.Translation;

namespace OfflineMode {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class OfflineModeMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.offlinemode";
        public const string Name = "OfflineMode";
        public const string Version = "0.3.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        internal static T t;

        public string GetModType() => "Utility";
        public string GetModName() => Name;
        public string GetModDescription() => t("mod.description");
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            OfflineModePatch.SetDisabled();
        }

        private void Awake() {
            PublicLogger = Logger;
            t = TranslationService.For(Name, Info.Location);

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
