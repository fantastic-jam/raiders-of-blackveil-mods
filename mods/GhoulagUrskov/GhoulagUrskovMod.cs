using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using GhoulagUrskov.Patch;

namespace GhoulagUrskov {
    [BepInPlugin(Id, Name, Version)]
    public class GhoulagUrskovMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.ghoulag-urskov";
        public const string Name = "GhoulagUrskov";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        private readonly Harmony _harmony = new Harmony(Id);

#if DEV_HOTRELOAD
        public static ConfigEntry<string> CfgDevHotReloadDllPath;
        private Harmony _devHarmony;
#endif

        public string GetModType() => "Mod";
        public string GetModName() => Name;
        public string GetModDescription() => "";
        public bool IsClientRequired => false;
        public bool Disabled => GhoulagUrskovPatch.Disabled;

        public void Enable() {
            GhoulagUrskovPatch.Disabled = false;
            GhoulagUrskovPatch.Patch(_harmony);
#if DEV_HOTRELOAD
            Dev.HotReloadController.Enable();
#endif
        }

        public void Disable() {
            GhoulagUrskovPatch.Disabled = true;
            GhoulagUrskovPatch.Unpatch();
#if DEV_HOTRELOAD
            Dev.HotReloadController.Disable();
#endif
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                if (!GhoulagUrskovPatch.Init()) {
                    PublicLogger.LogError($"{Name}: init failed — mod disabled.");
                    return;
                }
                Enable();
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }

#if DEV_HOTRELOAD
            CfgDevHotReloadDllPath = Config.Bind(
                "DevHotReload", "DllPath", "",
                "Absolute path to the Debug build output DLL for F9 hot-reload.");
            _devHarmony = new Harmony(Id + ".dev");
            Dev.HotReloadController.Initialize(_devHarmony, CfgDevHotReloadDllPath.Value);
            PublicLogger.LogWarning($"[HotReload] DEV BUILD. DLL: {CfgDevHotReloadDllPath.Value}");
#endif
        }
    }
}
