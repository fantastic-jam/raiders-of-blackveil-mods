using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using ModRegistry;

namespace ModManager {
    internal static class ModManagerRegistrants {
        internal static readonly List<RegisteredMod> Mods = new();
        internal static readonly List<RegisteredMod> Cheats = new();

        internal static void Scan() {
            Mods.Clear();
            Cheats.Clear();

            foreach (var info in Chainloader.PluginInfos.Values) {
                var mod = TryResolve(info);
                if (mod == null) { continue; }

                switch (mod.Type) {
                    case ModType.Cheat:
                        Cheats.Add(mod);
                        break;
                    case ModType.Mod:
                        Mods.Add(mod);
                        break;
                        // Cosmetics: tracked but not surfaced in session checkboxes (future feature)
                }

                ModManagerMod.PublicLogger.LogInfo(
                    $"ModManager: [{mod.Type}] {mod.Name} — {info.Metadata.Version}"
                );
            }

            ModManagerMod.PublicLogger.LogInfo(
                $"ModManager: {Cheats.Count} cheat mod(s), {Mods.Count} regular mod(s)."
            );
        }

        private static RegisteredMod TryResolve(PluginInfo info) {
            // Use 'is null' instead of '== null' to bypass Unity's overloaded == operator,
            // which returns true for destroyed native objects even with a valid C# reference.
            if (info.Instance is null) { return null; }

            // Interface takes priority — typed, zero-reflection call overhead.
            if (info.Instance is IModRegistrant r) {
                return BuildFromInterface(r, info);
            }

            // Duck typing: any plugin that exposes GetModType() + Disable() qualifies,
            // no ModRegistry.dll reference required.
            return TryBuildFromDuckTyping(info.Instance, info);
        }

        private static RegisteredMod BuildFromInterface(IModRegistrant r, PluginInfo info) {
            if (!TryParseModType(r.GetModType(), out var modType)) { return null; }

            var name = r.GetModName();
            if (string.IsNullOrEmpty(name)) { name = info.Metadata.Name; }

            return new RegisteredMod(modType, name, r.GetModDescription() ?? "", r.Disable);
        }

        private static RegisteredMod TryBuildFromDuckTyping(BaseUnityPlugin instance, PluginInfo info) {
            var type = instance.GetType();

            var getModType = type.GetMethod("GetModType", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var disable = type.GetMethod("Disable", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            if (getModType == null || disable == null) { return null; }
            if (!TryParseModType(getModType.Invoke(instance, null)?.ToString(), out var modType)) { return null; }

            var getModName = type.GetMethod("GetModName", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            var getModDesc = type.GetMethod("GetModDescription", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);

            var name = getModName?.Invoke(instance, null)?.ToString();
            if (string.IsNullOrEmpty(name)) { name = info.Metadata.Name; }

            var description = getModDesc?.Invoke(instance, null)?.ToString() ?? "";

            return new RegisteredMod(modType, name, description, () => disable.Invoke(instance, null));
        }

        private static bool TryParseModType(string raw, out ModType result) =>
            Enum.TryParse(raw, ignoreCase: true, out result);
    }
}
