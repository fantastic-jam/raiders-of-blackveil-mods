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

        private Harmony _harmony;

        // Duck typing — ModManager discovers these without a ModRegistry.dll reference
        public string GetModType() => "Utility";
        public string GetModName() => Name;
        public string GetModDescription() => "Fixes the <N/A> display name shown when your Steam profile is private.";
        public bool Disabled => PlayerNameFixPatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            PlayerNameFixPatch.SetDisabled();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            PlayerNameFixPatch.SetEnabled();
        }

        private void Awake() {
            PublicLogger = Logger;

            try {
                _harmony = new Harmony(Id);
                PlayerNameFixPatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
