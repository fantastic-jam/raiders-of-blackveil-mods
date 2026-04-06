using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using ModRegistry;
using UnityEngine.UIElements;

namespace ModManager {
    internal static class ModManagerRegistrants {
        // Session stepper lists — only Mod and Cheat types
        internal static readonly List<RegisteredMod> Mods = new();
        internal static readonly List<RegisteredMod> Cheats = new();

        // Everything discovered (managed or not), excluding ModManager itself
        internal static readonly List<RegisteredMod> AllDiscovered = new();

        internal static IEnumerable<RegisteredMod> AllMods() => AllDiscovered;

        internal static void Scan() {
            Mods.Clear();
            Cheats.Clear();
            AllDiscovered.Clear();

            foreach (var info in Chainloader.PluginInfos.Values) {
                // Skip ModManager itself to avoid self-listing
                if (info.Metadata.GUID == ModManagerMod.Id) { continue; }

                var mod = TryResolve(info);
                if (mod == null) {
                    // No IModRegistrant and no duck typing — list as unmanageable
                    AllDiscovered.Add(new RegisteredMod(info.Metadata.GUID, info.Metadata.Name));
                    ModManagerMod.PublicLogger.LogInfo(
                        $"ModManager: [unmanaged] {info.Metadata.Name} ({info.Metadata.GUID})"
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
                        // Cosmetics/Utility: in AllDiscovered (manageable), not in session steppers
                }

                ModManagerMod.PublicLogger.LogInfo(
                    $"ModManager: [{mod.Type}] {mod.Name} ({mod.Guid}) — {info.Metadata.Version}"
                );
            }

            ModManagerMod.PublicLogger.LogInfo(
                $"ModManager: {Cheats.Count} cheat(s), {Mods.Count} mod(s), " +
                $"{AllDiscovered.Count(m => !m.IsManaged)} unmanaged."
            );
        }

        internal static void ApplyStartupDisables() {
            foreach (var mod in AllDiscovered.Where(m => m.IsManaged)) {
                if (!ModManagerConfig.IsEnabled(mod.Guid)) {
                    ModManagerMod.PublicLogger.LogInfo($"ModManager: startup disable — {mod.Name}");
                    mod.Disable();
                }
            }
        }

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
                r.Disable, r.Enable, menuName, openMenu, closeMenu);
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

            return new RegisteredMod(modType, info.Metadata.GUID, name, description,
                () => disable.Invoke(instance, null),
                enable != null ? () => enable.Invoke(instance, null) : null,
                menuName, openMenu, closeMenu);
        }

        /// <summary>
        /// Checks for IModMenuProvider (interface) then duck-typed MenuName / OpenMenu / CloseMenu.
        /// Returns nulls if neither is found.
        /// </summary>
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
