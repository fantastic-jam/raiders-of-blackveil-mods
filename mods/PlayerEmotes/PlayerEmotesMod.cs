using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Fusion;
using HarmonyLib;
using ModRegistry;
using PlayerEmotes.Patch;
using WildguardModFramework.Network;
using WildguardModFramework.Translation;
using PlayerEmotes.UI;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace PlayerEmotes {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class PlayerEmotesMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.player-emotes";
        public const string Name = "PlayerEmotes";
        public const string Version = "0.1.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        public static ConfigEntry<Key> CfgEmoteKey;
        internal static T t;
        private readonly Harmony _harmony = new Harmony(Id);
        private PlayerEmotesMenu _menu;
        private static Action<PlayerRef, byte[]> _onEmote;

#if DEV_HOTRELOAD
        public static ConfigEntry<string> CfgDevHotReloadDllPath;
        private Harmony _devHarmony;
#endif

        public string GetModType() => nameof(ModType.Cosmetics);
        public string GetModName() => Name;
        public string GetModDescription() => t("mod.description");
        public bool IsClientRequired => false;
        public bool Disabled { get; private set; }

        public string MenuName => "Player Emotes";
        public void OpenMenu(VisualElement container, bool isInGameMenu) {
            _menu = new PlayerEmotesMenu(CfgEmoteKey, isInGameMenu);
            _menu.Build(container);
        }
        public void CloseMenu() {
            _menu?.Dispose();
            _menu = null;
        }
        public (string Title, Action<VisualElement, bool> Build)[] SubMenus => null;

        public void Enable() {
            Disabled = false;
            _onEmote = EmoteController.OnEmoteReceived;
            WmfNetwork.Subscribe("player-emotes.emote", _onEmote);
            PlayerEmotesPatch.Patch(_harmony);
#if DEV_HOTRELOAD
            Dev.HotReloadController.Enable();
#endif
        }

        public void Disable() {
            Disabled = true;
            WmfNetwork.Unsubscribe("player-emotes.emote", _onEmote);
            PlayerEmotesPatch.Unpatch();
#if DEV_HOTRELOAD
            Dev.HotReloadController.Disable();
#endif
        }

        private void Awake() {
            PublicLogger = Logger;
            t = TranslationService.For(Name, Info.Location);
            CfgEmoteKey = Config.Bind("Controls", "EmoteKey", Key.T, "Key to send an emote.");
            try {
                if (!PlayerEmotesPatch.Init()) {
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
                "Absolute path to the Debug build output DLL for F9 hot-reload. Example: C:/projects/.../mods/PlayerEmotes/bin/Debug/PlayerEmotes.dll");
            _devHarmony = new Harmony(Id + ".dev");
            Dev.HotReloadController.Initialize(_devHarmony, CfgDevHotReloadDllPath.Value);
            PublicLogger.LogWarning($"[HotReload] DEV BUILD. DLL: {CfgDevHotReloadDllPath.Value}");
#endif
        }
    }
}
