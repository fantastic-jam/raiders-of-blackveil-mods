using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BeginnersWelcome.Network;
using BeginnersWelcome.Patch;
using BeginnersWelcome.UI;
using HarmonyLib;
using ModRegistry;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BeginnersWelcome {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class BeginnersWelcomeMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.beginnerswelcome";
        public const string Name = "BeginnersWelcome";
        public const string Version = "0.4.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        public static ConfigEntry<Key> PanelToggleKey;

        private Harmony _harmony;
        private BeginnersWelcomeMenu _menu;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public string GetModDescription() => "Lets the host set a per-player handicap so different skill levels can share a run.";
        public bool IsClientRequired => false;
        public bool Disabled => BeginnersWelcomePatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            BeginnersWelcomePatch.SetDisabled();
            HandicapNetwork.Disable();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            BeginnersWelcomePatch.SetEnabled();
            HandicapNetwork.Enable();
        }

        public string MenuName => "Beginners Welcome";

        public void OpenMenu(VisualElement container, bool isInGameMenu) {
            _menu = new BeginnersWelcomeMenu(PanelToggleKey, isInGameMenu);
            _menu.Build(container);
        }

        public void CloseMenu() {
            _menu?.Dispose();
            _menu = null;
        }
        public (string Title, Action<VisualElement, bool> Build)[] SubMenus => null;

        public void Awake() {
            PublicLogger = Logger;
            PanelToggleKey = Config.Bind("Controls", "PanelToggleKey", Key.F3,
                "Key that toggles the handicap panel in-game.");
            try {
                _harmony = new Harmony(Id);
                BeginnersWelcomePatch.Apply(_harmony);
                HandicapNetwork.Enable();
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
