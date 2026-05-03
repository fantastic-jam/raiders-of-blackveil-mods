using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HandyPurse.Bank;
using HandyPurse.Patch;
using HarmonyLib;
using ModRegistry;
using UnityEngine;
using UnityEngine.UIElements;
using WildguardModFramework.Translation;

namespace HandyPurse {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class HandyPurseMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.handypurse";
        public const string Name = "HandyPurse";
        public const string Version = "0.7.0";
        public const string Author = "christphe";
        private const string BuiltAgainstGameVersion = "0.1.0_WIN_2026-01-29_180103_202c53513d";

        public static ManualLogSource PublicLogger;
        internal static T t;

        private static ConfigEntry<int> _scrapCap;
        private static ConfigEntry<int> _blackCoinCap;
        private static ConfigEntry<int> _crystalCap;
        private static ConfigEntry<bool> _strictVersionChecking;
        private static ConfigEntry<string> _overrideVersionCheck;

        public static int ScrapCap => Math.Max(1, _scrapCap?.Value ?? 9999);
        public static int BlackCoinCap => Math.Max(1, _blackCoinCap?.Value ?? 999);
        public static int CrystalCap => Math.Max(1, _crystalCap?.Value ?? 999);

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public string GetModDescription() => t("mod.description");
        public bool IsClientRequired => false;
        public bool Disabled => HandyPursePatch.Disabled;
        public void Disable() {
            PublicLogger.LogInfo($"{Name}: disabled.");
            HandyPursePatch.SetDisabled();
        }
        public void Enable() {
            PublicLogger.LogInfo($"{Name}: enabled.");
            HandyPursePatch.SetEnabled();
        }

        // IModMenuProvider — exposes an in-game panel via ModManager
        public string MenuName => Name;
        public void OpenMenu(VisualElement container, bool isInGameMenu) => HandyPurseMenu.Open(container, isInGameMenu);
        public void CloseMenu() => HandyPurseMenu.Close();
        public (string Title, Action<VisualElement, bool> Build)[] SubMenus => null;

        private void Awake() {
            PublicLogger = Logger;
            t = TranslationService.For(Name, Info.Location);
            PurseBank.OverrideDataDir(System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "data", "HandyPurse"));
            PurseBank.Warn = msg => Logger.LogWarning(msg);
            PurseBank.Error = msg => Logger.LogError(msg);
            PurseBank.DeleteLegacyTopup();

            try {
                BindConfig();
                _harmony = new Harmony(Id);
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
                return;
            }

            // Register the main menu hook before anything that can fail so popups always fire.
            HandyPursePatch.ApplyMenuHook(_harmony);

            var gameVersion = Application.version;
            var overrideVersion = _overrideVersionCheck?.Value ?? string.Empty;
            var versionOk = gameVersion == BuiltAgainstGameVersion
                || (!string.IsNullOrWhiteSpace(overrideVersion) && gameVersion == overrideVersion);

            if (!versionOk) {
                if (!string.IsNullOrWhiteSpace(overrideVersion)) {
                    PublicLogger.LogWarning(
                        $"HandyPurse: override version '{overrideVersion}' does not match running game v{gameVersion}.");
                }

                if (_strictVersionChecking?.Value ?? true) {
                    HandyPursePatch.PendingVersionMismatchPopup = true;
                    LogVersionMismatch(gameVersion);
                } else {
                    PublicLogger.LogWarning(
                        $"HandyPurse: built against game v{BuiltAgainstGameVersion}, " +
                        $"running on v{gameVersion} — patches may fail. Update the mod if issues occur.");
                }
            }

            if (!HandyPursePatch.Apply(_harmony)) {
                HandyPursePatch.SetDisabled();
                HandyPursePatch.PendingBreakingChangePopup = true;
                LogBreakingChange();
                // Throw so Unity logs a visible red error on top of the LogFatal messages.
                // The game and BepInEx continue loading — only this plugin is dead.
                throw new Exception(
                    $"[{Name} v{Version}] Stack limits NOT active. " +
                    $"Currencies above vanilla caps WILL be clamped on next save. " +
                    $"Do NOT uninstall until stacks are within vanilla limits.");
            }

            try {
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded. Game: {Application.version}");
                PublicLogger.LogInfo($"Caps: Scrap={ScrapCap}, BlackCoin={BlackCoinCap}, Crystal={CrystalCap}");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }

        private void LogVersionMismatch(string gameVersion) {
            PublicLogger.LogFatal("============================================================");
            PublicLogger.LogFatal($"{Name} v{Version}: game version mismatch.");
            PublicLogger.LogFatal($"Expected: {BuiltAgainstGameVersion}");
            PublicLogger.LogFatal($"Running:  {gameVersion}");
            PublicLogger.LogFatal($"Patches applied — set OverrideVersionCheck in the BepInEx config");
            PublicLogger.LogFatal($"to silence this warning once the community confirms compatibility.");
            PublicLogger.LogFatal("============================================================");
        }

        private void LogBreakingChange() {
            PublicLogger.LogFatal("============================================================");
            PublicLogger.LogFatal($"{Name} v{Version}: game assembly breaking change detected.");
            PublicLogger.LogFatal($"Mod DISABLED — stack limits are NOT active. Any currencies");
            PublicLogger.LogFatal($"above vanilla caps will be clamped on next save. Do not");
            PublicLogger.LogFatal($"uninstall until your stacks are back within vanilla limits.");
            PublicLogger.LogFatal($"Update the mod or report a bug (include your BepInEx log).");
            PublicLogger.LogFatal("============================================================");
        }

        private void BindConfig() {
            _scrapCap = Config.Bind("Limits", "ScrapCap", 9999, "Max stack for Scrap.");
            _blackCoinCap = Config.Bind("Limits", "BlackCoinCap", 999, "Max stack for Black Coin.");
            _crystalCap = Config.Bind("Limits", "CrystalCap", 999, "Max stack for Crystals (BlackBlood and Glitter).");
            _strictVersionChecking = Config.Bind("Compatibility", "StrictVersionChecking", true,
                "If true, shows a warning popup when the game version does not match the version this mod was built against. " +
                "Patches are always applied regardless of this setting.");
            _overrideVersionCheck = Config.Bind("Compatibility", "OverrideVersionCheck", string.Empty,
                "If the community confirms HandyPurse works on a newer game version, paste that version string here " +
                "to silence the version mismatch warning. Leave empty to use the built-in known-good version only.");
        }
    }
}
