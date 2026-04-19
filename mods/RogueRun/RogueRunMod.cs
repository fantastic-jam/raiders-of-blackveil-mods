using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using RogueRun.Patch;

namespace RogueRun {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class RogueRunMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.roguerun";
        public const string Name = "RogueRun";
        public const string Version = "0.1.0";
        public const string Author = "christphe";
        private const string TargetGameVersion = "0.1.0_WIN_2026-01-29_180103_202c53513d";

        public static ManualLogSource PublicLogger;

        public string GetModType() => nameof(ModType.GameMode);
        public string GetModName() => Name;
        public string GetModDescription() => "The run your grandfather still talks about. Go in empty, clear every room, walk out clean. Nothing left but the memory.";
        public bool IsClientRequired => true;

        // Shown once to modded clients when joining a RogueRun session (every time for unmodded clients).
        public string GetJoinMessage() =>
            "This session is running RogueRun.\n\nYour equipped items and inventory will be moved to the Smuggler Backpack for safekeeping. Pick them up from the smuggler in the lobby when the run is over.";

        // Shown as a HUD corner notification on the first level of a run.
        public string GetRunStartNotification() =>
            "Went in empty. Clear every room. Walk out clean.\nYour loadout is stashed with the smuggler — pick it up when you return.";

        public bool Disabled => !RogueRunState.IsActive;

        public void Enable() {
            RogueRunState.IsActive = true;
            PublicLogger.LogInfo($"{Name}: enabled.");
        }

        public void Disable() {
            RogueRunState.IsActive = false;
            PublicLogger.LogInfo($"{Name}: disabled.");
        }

        private Harmony _harmony;

        private void Awake() {
            PublicLogger = Logger;

            try {
                if (!CheckGameVersion()) {
                    return;
                }

                _harmony = new Harmony(Id);
                if (!RogueRunPatch.Apply(_harmony)) {
                    _harmony.UnpatchSelf();
                    RogueRunState.IsActive = false;
                    LogBreakingChange();
                    return;
                }
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }

        private bool CheckGameVersion() {
            try {
                var versionFile = Path.Combine(Paths.GameRootPath, "version.txt");
                if (!File.Exists(versionFile)) {
                    return true;
                }

                var gameVersion = File.ReadAllText(versionFile).Trim();
                if (gameVersion == TargetGameVersion) {
                    return true;
                }

                PublicLogger.LogError("============================================================");
                PublicLogger.LogError($"{Name} v{Version}: wrong game version.");
                PublicLogger.LogError($"  Expected: {TargetGameVersion}");
                PublicLogger.LogError($"  Found:    {gameVersion}");
                PublicLogger.LogError("Mod disabled. Update the mod or report a bug (include log).");
                PublicLogger.LogError("============================================================");
                return false;
            }
            catch {
                return true; // can't read version file — let patch reflection checks catch real breakage
            }
        }

        private void LogBreakingChange() {
            PublicLogger.LogError("============================================================");
            PublicLogger.LogError($"{Name} v{Version}: game assembly breaking change detected.");
            PublicLogger.LogError($"Mod disabled. Update the mod or report a bug (include log).");
            PublicLogger.LogError("============================================================");
        }
    }
}
