using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JoinAnytime.Patch;

namespace JoinAnytime {
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    [BepInPlugin(Id, Name, Version)]
    public class JoinAnytimeMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.joinanytime";
        public const string Name = "JoinAnytime";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        private readonly Harmony _harmony = new Harmony(Id);

#if DEV_HOTRELOAD
        public static ConfigEntry<string> CfgDevHotReloadDllPath;
        private Harmony _devHarmony;
#endif

        // WMF duck-typing — WMF discovers these via reflection; no ModRegistry reference needed.
        public string GetModType() => "Mod";
        public string GetModName() => Name;
        public string GetModDescription() => "Lets anyone join a run in progress: they spawn next to the host with XP and perks matched to the party. Host-only.";
        public bool IsClientRequired => false;
        public bool Disabled => JoinAnytimePatch.Disabled;

        public void Enable() {
            JoinAnytimePatch.Disabled = false;
            JoinAnytimePatch.Patch(_harmony);
#if DEV_HOTRELOAD
            Dev.HotReloadController.Enable();
#endif
        }

        public void Disable() {
            JoinAnytimePatch.Disabled = true;
            JoinAnytimePatch.Unpatch();
#if DEV_HOTRELOAD
            Dev.HotReloadController.Disable();
#endif
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                if (!JoinAnytimePatch.Init()) {
                    PublicLogger.LogError($"{Name}: init failed — mod disabled.");
                    return;
                }
                WmfChatBridge.Resolve();
                Enable();
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }

#if DEV_HOTRELOAD
            CfgDevHotReloadDllPath = Config.Bind(
                "DevHotReload", "DllPath", "",
                "Absolute path to the Debug build output DLL for F9 hot-reload. Example: C:/projects/.../mods/JoinAnytime/bin/Debug/JoinAnytime.dll");
            _devHarmony = new Harmony(Id + ".dev");
            Dev.HotReloadController.Initialize(_devHarmony, CfgDevHotReloadDllPath.Value);
            PublicLogger.LogWarning($"[HotReload] DEV BUILD. DLL: {CfgDevHotReloadDllPath.Value}");
#endif
        }
    }
}
