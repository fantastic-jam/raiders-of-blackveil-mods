using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using JoinAnytime.Patch;

namespace JoinAnytime {
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    [BepInPlugin(Id, Name, Version)]
    public class JoinAnytimeMod : BasePlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.joinanytime";
        public const string Name = "JoinAnytime";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        private readonly Harmony _harmony = new Harmony(Id);

        // WMF duck-typing — WMF discovers these via reflection; no ModRegistry reference needed.
        public string GetModType() => "Mod";
        public string GetModName() => Name;
        public string GetModDescription() => "Lets anyone join a run in progress: they spawn next to the host with XP and perks matched to the party. Host-only.";
        public bool IsClientRequired => false;
        public bool Disabled => JoinAnytimePatch.Disabled;

        public void Enable() {
            JoinAnytimePatch.Disabled = false;
            JoinAnytimePatch.Patch(_harmony);
        }

        public void Disable() {
            JoinAnytimePatch.Disabled = true;
            JoinAnytimePatch.Unpatch();
        }

        public override void Load() {
            PublicLogger = Log;
            try {
                WmfChatBridge.Resolve();
                Enable();
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
