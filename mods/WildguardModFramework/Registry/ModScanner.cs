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
        internal static readonly List<RegisteredMod> Mods = new();
        internal static readonly List<RegisteredMod> Cheats = new();
        internal static readonly List<RegisteredGameMode> GameModes = new();
        internal static string SelectedGameModeVariantId { get; set; }
        internal static readonly List<RegisteredMod> AllDiscovered = new();
        internal static IEnumerable<RegisteredMod> AllMods() => AllDiscovered;

        internal static void Scan() {
            Mods.Clear();
            Cheats.Clear();
            GameModes.Clear();
            AllDiscovered.Clear();

            foreach (var info in Chainloader.PluginInfos.Values) {
                if (info.Metadata.GUID == WmfMod.Id) { continue; }

                var mod = TryResolve(info);
                if (mod == null) {
                    AllDiscovered.Add(new RegisteredMod(info.Metadata.GUID, info.Metadata.Name));
                    WmfMod.PublicLogger.LogInfo($"WMF: [unmanaged] {info.Metadata.Name} ({info.Metadata.GUID})");
                    continue;
                }

                AllDiscovered.Add(mod);

                switch (mod.Type) {
                    case ModType.Cheat: Cheats.Add(mod); break;
                    case ModType.Mod: Mods.Add(mod); break;
                    case ModType.GameMode: AddGameModesFrom(mod, info); break;
                }

                WmfMod.PublicLogger.LogInfo($"WMF: [{mod.Type}] {mod.Name} ({mod.Guid}) — {info.Metadata.Version}");
            }

            var savedId = WmfConfig.ActiveGameModeId;
            SelectedGameModeVariantId = GameModes.Any(g => g.VariantId == savedId) ? savedId : null;

            WmfMod.PublicLogger.LogInfo(
                $"WMF: {Cheats.Count} cheat(s), {Mods.Count} mod(s), " +
                $"{GameModes.Count} game mode(s), {AllDiscovered.Count(m => !m.IsManaged)} unmanaged.");
        }

        // ── Game mode registration ─────────────────────────────────────────

        private static void AddGameModesFrom(RegisteredMod mod, PluginInfo info) {
            if (info.Instance is IGameModeProvider provider) {
                foreach (var variant in provider.GameModeVariants) {
                    var variantId = $"{info.Metadata.GUID}::{variant.VariantId}";
                    GameModes.Add(new RegisteredGameMode(
                        variantId, variant.DisplayName, variant.Description ?? "", info.Metadata.GUID,
                        enable: () => { mod.Enable(); provider.EnableVariant(variant.VariantId); },
                        disable: mod.Disable,
                        isClientRequired: mod.IsClientRequired,
                        joinMessage: variant.JoinMessage,
                        runStartMessage: variant.RunStartMessage));
                    WmfMod.PublicLogger.LogInfo($"WMF:   [GameMode variant] {variant.DisplayName} ({variantId})");
                }
            } else {
                var p = new ModProxy(info.Instance);
                GameModes.Add(new RegisteredGameMode(
                    info.Metadata.GUID, mod.Name, mod.Description, info.Metadata.GUID,
                    enable: mod.Enable, disable: mod.Disable,
                    isClientRequired: mod.IsClientRequired,
                    joinMessage: p.GetJoinMessage(),
                    runStartMessage: p.GetRunStartNotification()));
            }
        }

        // ── Resolution ─────────────────────────────────────────────────────

        private static RegisteredMod TryResolve(PluginInfo info) {
            if (info.Instance is null) { return null; }

            var p = new ModProxy(info.Instance);
            if (!p.IsManaged) { return null; }

            if (!TryParseModType(p.GetModType(), out var modType)) { return null; }

            string name = p.GetModName();
            if (string.IsNullOrEmpty(name)) { name = info.Metadata.Name; }

            return new RegisteredMod(modType, info.Metadata.GUID, name, p.GetModDescription(),
                p.Disable, p.HasEnable ? p.Enable : null,
                p.MenuName, p.HasMenu ? p.OpenMenu : null, p.HasMenu ? p.CloseMenu : null,
                isClientRequired: p.IsClientRequired, subMenus: p.SubMenus);
        }

        private static bool TryParseModType(string raw, out ModType result) =>
            Enum.TryParse(raw, ignoreCase: true, out result);

        // ── Proxy ──────────────────────────────────────────────────────────

        private sealed class ModProxy {
            private readonly object _obj;
            private readonly Type _type;
            private const BindingFlags Pub = BindingFlags.Instance | BindingFlags.Public;

            internal ModProxy(object obj) { _obj = obj; _type = obj.GetType(); }

            internal bool IsManaged =>
                _type.GetMethod("GetModType", Pub, null, Type.EmptyTypes, null) != null &&
                _type.GetMethod("Disable", Pub, null, Type.EmptyTypes, null) != null;

            internal string GetModType() => Str("GetModType");
            internal string GetModName() => Str("GetModName");
            internal string GetModDescription() => Str("GetModDescription") ?? "";
            internal string GetJoinMessage() => Str("GetJoinMessage");
            internal string GetRunStartNotification() => Str("GetRunStartNotification");

            internal bool IsClientRequired => Bool("IsClientRequired");
            internal bool HasEnable => _type.GetMethod("Enable", Pub, null, Type.EmptyTypes, null) != null;

            internal Action Disable => () => _type.GetMethod("Disable", Pub, null, Type.EmptyTypes, null).Invoke(_obj, null);
            internal Action Enable => () => _type.GetMethod("Enable", Pub, null, Type.EmptyTypes, null).Invoke(_obj, null);

            internal bool HasMenu => MenuName != null;
            internal string MenuName => PropStr("MenuName");

            internal Action<VisualElement, bool> OpenMenu => (c, g) =>
                _type.GetMethod("OpenMenu", Pub, null, new[] { typeof(VisualElement), typeof(bool) }, null)
                     ?.Invoke(_obj, new object[] { c, g });

            internal Action CloseMenu => () =>
                _type.GetMethod("CloseMenu", Pub, null, Type.EmptyTypes, null)?.Invoke(_obj, null);

            internal (string Title, Action<VisualElement, bool> Build)[] SubMenus {
                get {
                    var prop = _type.GetProperty("SubMenus", Pub);
                    if (prop == null) { return null; }
                    try { return ((string Title, Action<VisualElement, bool> Build)[])prop.GetValue(_obj); }
                    catch (Exception) { return null; }
                }
            }

            private string Str(string method) =>
                _type.GetMethod(method, Pub, null, Type.EmptyTypes, null)?.Invoke(_obj, null)?.ToString();

            private string PropStr(string prop) =>
                _type.GetProperty(prop, Pub)?.GetValue(_obj)?.ToString();

            private bool Bool(string prop) {
                var v = _type.GetProperty(prop, Pub)?.GetValue(_obj);
                return v is bool b && b;
            }
        }
    }
}
