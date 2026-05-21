using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HandyPurse.Bank;
using HandyPurse.Patch;
using HarmonyLib;
using ModRegistry;
using UnityEngine.UIElements;
using WildguardModFramework.Translation;

namespace HandyPurse {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class HandyPurseMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.handypurse";
        public const string Name = "HandyPurse";
        public const string Version = "1.0.0";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;
        internal static T t;

        private bool _disabled;
        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public string GetModDescription() => t("mod.description");
        public bool IsClientRequired => false;
        public bool Disabled => _disabled;
        public void Disable() { _disabled = true; PublicLogger.LogInfo($"{Name}: disabled."); }
        public void Enable() { _disabled = false; PublicLogger.LogInfo($"{Name}: enabled."); }

        public string MenuName => Name;
        public void OpenMenu(VisualElement container, bool isInGameMenu) => HandyPurseMenu.Open(container, isInGameMenu);
        public void CloseMenu() => HandyPurseMenu.Close();
        public (string Title, Action<VisualElement, bool> Build)[] SubMenus => null;

        private void Awake() {
            PublicLogger = Logger;
            t = TranslationService.For(Name, Info.Location);
            PurseBank.OverrideDataDir(Path.Combine(BepInEx.Paths.BepInExRootPath, "data", "HandyPurse"));
            PurseBank.Warn = msg => Logger.LogWarning(msg);
            PurseBank.Error = msg => Logger.LogError(msg);
            PurseBank.MigrateAllTopupsToBank();
            _harmony = new Harmony(Id);
            HandyPursePatch.ApplyMenuHook(_harmony);
            Logger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
        }
    }
}
