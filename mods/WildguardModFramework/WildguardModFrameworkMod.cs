using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using UnityEngine;
using UnityEngine.UIElements;
using WildguardModFramework.Chat;
using WildguardModFramework.Fixes;
using WildguardModFramework.ModMenu;
using WildguardModFramework.Network;
using WildguardModFramework.Notifications;
using WildguardModFramework.PlayerManagement;
using WildguardModFramework.Translation;

namespace WildguardModFramework {
    [BepInPlugin(Id, Name, Version)]
    public class WmfMod : BaseUnityPlugin, IModRegistrant {
        internal const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework";
        public const string Name = "WMF";
        public const string Version = "0.6.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        internal static WmfMod Instance { get; private set; }
        internal static T t;

        /// <summary>
        /// Persistent coroutine host. Created in Awake() with DontDestroyOnLoad so it survives
        /// scene transitions and is always available for networking coroutines.
        /// </summary>
        internal static CoroutineRunner Runner { get; private set; }

        private Harmony _harmony;
#if DEV_HOTRELOAD
        public static ConfigEntry<string> CfgDevHotReloadDllPath;
        private Harmony _devHarmony;
#endif

        public string GetModType() => nameof(ModType.Utility);
        public string GetModName() => Name;
        public string GetModDescription() => "Wildguard Mod Framework — mod management and session networking.";
        public bool Disabled { get; private set; }
        public bool IsClientRequired => false;
        public void Enable() {
            Disabled = false;
#if DEV_HOTRELOAD
            Dev.HotReloadController.Enable();
#endif
        }

        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            Disabled = true;
            _harmony?.UnpatchSelf();
#if DEV_HOTRELOAD
            Dev.HotReloadController.Disable();
#endif
        }

        // IModMenuProvider — WMF's own settings appear in the Mods menu left nav via ModScanner
        public string MenuName => "WMF";
        public void OpenMenu(VisualElement container, bool isInGameMenu) { }
        public void CloseMenu() { }
        public (string Title, Action<VisualElement, bool> Build)[] SubMenus => new (string Title, Action<VisualElement, bool> Build)[] {
            ("Chat",    (c, g) => ServerChat.BuildSettingsPanel(c, g)),
            ("Players", (c, g) => PlayerManagementController.BuildBanListPanel(c, g)),
        };

        public void Awake() {
            Instance = this;
            PublicLogger = Logger;
            try {
                var runnerGo = new GameObject("WMF.CoroutineRunner");
                DontDestroyOnLoad(runnerGo);
                Runner = runnerGo.AddComponent<CoroutineRunner>();
                WmfNotifications.Init(runnerGo.AddComponent<ModNotificationOverlay>());

                t = TranslationService.For(Name, Info.Location);
                _harmony = new Harmony(Id);
                WmfConfig.Init(Config);
                ServerChat.Init(_harmony);
                PlayerManagementController.Init(Config, _harmony);
                HostNameSync.Apply(_harmony); // game bug workaround — remove once fixed upstream
                GameModeProtocol.Init();
                NetworkPatch.Apply(_harmony);
                HostStartPagePatch.Apply(_harmony);
                MenuStartPagePatch.Apply(_harmony);
                MenuPausePagePatch.Apply(_harmony);
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
#if DEV_HOTRELOAD
            CfgDevHotReloadDllPath = Config.Bind(
                "DevHotReload", "DllPath", "",
                "Absolute path to the Debug build output DLL for F9 hot-reload. Example: C:/projects/.../mods/WildguardModFramework/bin/Debug/WildguardModFramework.dll");
            _devHarmony = new Harmony(Id + ".dev");
            Dev.HotReloadController.Initialize(_devHarmony, CfgDevHotReloadDllPath.Value);
            Dev.HotReloadController.Enable();
            PublicLogger.LogWarning($"[HotReload] DEV BUILD. DLL: {CfgDevHotReloadDllPath.Value}");
#endif
        }

    }
}
