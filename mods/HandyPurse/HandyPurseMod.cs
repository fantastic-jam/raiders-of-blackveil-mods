using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HandyPurse.Patch;
using HarmonyLib;
using ModRegistry;
using UnityEngine.UIElements;

namespace HandyPurse {
    [BepInPlugin(Id, Name, Version)]
    public class HandyPurseMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.handypurse";
        public const string Name = "HandyPurse";
        public const string Version = "0.3.1";
        public const string Author = "christphe";

        public static ManualLogSource PublicLogger;

        private static ConfigEntry<int> _scrapCap;
        private static ConfigEntry<int> _blackCoinCap;
        private static ConfigEntry<int> _crystalCap;

        public static int ScrapCap => Math.Max(1, _scrapCap?.Value ?? 9999);
        public static int BlackCoinCap => Math.Max(1, _blackCoinCap?.Value ?? 999);
        public static int CrystalCap => Math.Max(1, _crystalCap?.Value ?? 999);

        private Harmony _harmony;

        public string GetModType() => nameof(ModType.Mod);
        public string GetModName() => Name;
        public string GetModDescription() => "Raises the stack limits for all currencies. Caps are configurable in the BepInEx config file.";
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

        private void Awake() {
            PublicLogger = Logger;

            try {
                BindConfig();
                _harmony = new Harmony(Id);
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
                return;
            }

            // Register the main menu hook before Apply() so the popup fires even on critical failure.
            HandyPursePatch.ApplyMenuHook(_harmony);

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
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
                PublicLogger.LogInfo($"Caps: Scrap={ScrapCap}, BlackCoin={BlackCoinCap}, Crystal={CrystalCap}");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
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
        }
    }
}
