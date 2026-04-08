using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using ModRegistry;
using UnityEngine.UIElements;

namespace WildguardModFramework.Registry {
    internal static class ModScanner {
        // Session stepper lists — only Mod and Cheat types
        internal static readonly List<RegisteredMod> Mods = new();
        internal static readonly List<RegisteredMod> Cheats = new();

        // All registered game mode variants (may come from multiple plugins)
        internal static readonly List<RegisteredGameMode> GameModes = new();

        // In-memory selected variant ID (null = Normal). Shared between host page and solo modal.
        // Initialised from config on each Scan(); written on stepper change and persisted in BeginPlaySessionPrefix.
        internal static string SelectedGameModeVariantId { get; set; }

        // Everything discovered (managed or not), excluding WMF itself
        internal static readonly List<RegisteredMod> AllDiscovered = new();

        internal static IEnumerable<RegisteredMod> AllMods() => AllDiscovered;

        internal static void Scan() {
            Mods.Clear();
            Cheats.Clear();
            GameModes.Clear();
            AllDiscovered.Clear();

            foreach (var info in Chainloader.PluginInfos.Values) {
                // Skip WMF itself to avoid self-listing
                if (info.Metadata.GUID == WmfMod.Id) { continue; }

                var mod = TryResolve(info);
                if (mod == null) {
                    // No IModRegistrant and no duck typing — list as unmanageable
                    AllDiscovered.Add(new RegisteredMod(info.Metadata.GUID, info.Metadata.Name));
                    WmfMod.PublicLogger.LogInfo(
                        $"WMF: [unmanaged] {info.Metadata.Name} ({info.Metadata.GUID})"
                    );
                    continue;
                }

                AllDiscovered.Add(mod);

                switch (mod.Type) {
                    case ModType.Cheat:
                        Cheats.Add(mod);
                        break;
                    case ModType.Mod:
                        Mods.Add(mod);
                        break;
                    case ModType.GameMode:
                        AddGameModesFrom(mod, info);
                        break;
                        // Cosmetics/Utility: in AllDiscovered (manageable), not in session steppers
                }

                WmfMod.PublicLogger.LogInfo(
                    $"WMF: [{mod.Type}] {mod.Name} ({mod.Guid}) — {info.Metadata.Version}"
                );
            }

            // Validate and restore the selected game mode from config
            var savedId = WmfConfig.ActiveGameModeId;
            SelectedGameModeVariantId = GameModes.Any(g => g.VariantId == savedId) ? savedId : null;

            WmfMod.PublicLogger.LogInfo(
                $"WMF: {Cheats.Count} cheat(s), {Mods.Count} mod(s), " +
                $"{GameModes.Count} game mode(s), " +
                $"{AllDiscovered.Count(m => !m.IsManaged)} unmanaged."
            );
        }

        // ── Game mode registration ─────────────────────────────────────────

        private static void AddGameModesFrom(RegisteredMod mod, PluginInfo info) {
            if (info.Instance is IGameModeProvider provider) {
                // Multi-variant: register one entry per variant
                foreach (var variant in provider.GameModeVariants) {
                    var variantId = $"{info.Metadata.GUID}::{variant.VariantId}";
                    GameModes.Add(new RegisteredGameMode(
                        variantId, variant.DisplayName, variant.Description ?? "", info.Metadata.GUID,
                        enable: () => { mod.Enable(); provider.EnableVariant(variant.VariantId); },
                        disable: mod.Disable,
                        isClientRequired: mod.IsClientRequired,
                        joinMessage: variant.JoinMessage,
                        runStartMessage: variant.RunStartMessage
                    ));
                    WmfMod.PublicLogger.LogInfo(
                        $"WMF:   [GameMode variant] {variant.DisplayName} ({variantId})"
                    );
                }
            } else {
                // Single-variant: the whole mod is one game mode entry
                GameModes.Add(new RegisteredGameMode(
                    info.Metadata.GUID, mod.Name, mod.Description, info.Metadata.GUID,
                    enable: mod.Enable,
                    disable: mod.Disable,
                    isClientRequired: mod.IsClientRequired,
                    joinMessage: TryGetStringFromMethod(info.Instance, "GetJoinMessage"),
                    runStartMessage: TryGetStringFromMethod(info.Instance, "GetRunStartNotification")
                ));
            }
        }

        private static string TryGetStringFromMethod(BaseUnityPlugin instance, string methodName) {
            if (instance == null) { return null; }
            var method = instance.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            return method?.Invoke(instance, null)?.ToString();
        }

        // ── Resolution helpers ─────────────────────────────────────────────

        private static RegisteredMod TryResolve(PluginInfo info) {
            if (info.Instance is null) { return null; }

            if (info.Instance is IModRegistrant r) {
                return BuildFromInterface(r, info);
            }

            return TryBuildFromDuckTyping(info.Instance, info);
        }

        private static RegisteredMod BuildFromInterface(IModRegistrant r, PluginInfo info) {
            if (!TryParseModType(r.GetModType(), out var modType)) { return null; }

            var name = r.GetModName();
            if (string.IsNullOrEmpty(name)) { name = info.Metadata.Name; }

            var (menuName, openMenu, closeMenu) = TryGetMenuProvider(info.Instance);

            return new RegisteredMod(modType, info.Metadata.GUID, name, r.GetModDescription() ?? "",
                r.Disable, r.Enable, menuName, openMenu, closeMenu, isClientRequired: r.IsClientRequired);
        }

        private static RegisteredMod TryBuildFromDuckTyping(BaseUnityPlugin instance, PluginInfo info) {
            var type = instance.GetType();

            var getModType = type.GetMethod("GetModType", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var disable = type.GetMethod("Disable", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            if (getModType == null || disable == null) { return null; }
            if (!TryParseModType(getModType.Invoke(instance, null)?.ToString(), out var modType)) { return null; }

            var getModName = type.GetMethod("GetModName", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var getModDesc = type.GetMethod("GetModDescription", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var enable = type.GetMethod("Enable", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            var name = getModName?.Invoke(instance, null)?.ToString();
            if (string.IsNullOrEmpty(name)) { name = info.Metadata.Name; }

            var description = getModDesc?.Invoke(instance, null)?.ToString() ?? "";

            var (menuName, openMenu, closeMenu) = TryGetMenuProvider(instance);

            var isClientRequiredProp = type.GetProperty("IsClientRequired", BindingFlags.Instance | BindingFlags.Public);
            bool isClientRequired = isClientRequiredProp != null && (bool)(isClientRequiredProp.GetValue(instance) ?? false);

            return new RegisteredMod(modType, info.Metadata.GUID, name, description,
                () => disable.Invoke(instance, null),
                enable != null ? () => enable.Invoke(instance, null) : null,
                menuName, openMenu, closeMenu, isClientRequired: isClientRequired);
        }

        private static (string menuName, Action<VisualElement, bool> openMenu, Action closeMenu) TryGetMenuProvider(BaseUnityPlugin instance) {
            if (instance is IModMenuProvider p) {
                return (p.MenuName, (c, g) => p.OpenMenu(c, g), p.CloseMenu);
            }

            var type = instance.GetType();
            var menuNameProp = type.GetProperty("MenuName", BindingFlags.Instance | BindingFlags.Public);
            var menuName = menuNameProp?.GetValue(instance)?.ToString();

            if (string.IsNullOrEmpty(menuName)) { return (null, null, null); }

            var openMenu = type.GetMethod("OpenMenu", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(VisualElement), typeof(bool) }, null);
            var closeMenu = type.GetMethod("CloseMenu", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            if (openMenu == null || closeMenu == null) { return (null, null, null); }

            return (
                menuName,
                (container, isInGameMenu) => openMenu.Invoke(instance, new object[] { container, isInGameMenu }),
                () => closeMenu.Invoke(instance, null)
            );
        }

        private static bool TryParseModType(string raw, out ModType result) =>
            Enum.TryParse(raw, ignoreCase: true, out result);
    }
}
