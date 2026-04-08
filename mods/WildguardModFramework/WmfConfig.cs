using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using WildguardModFramework.Registry;

namespace WildguardModFramework {
    internal static class WmfConfig {
        private static ConfigFile _cfg;
        private static ConfigEntry<string> _activeGameMode;

        internal static void Init(ConfigFile cfg) => _cfg = cfg;

        // ── Per-mod enabled/disabled ───────────────────────────────────────

        internal static ConfigEntry<bool> GetEntry(string guid) =>
            _cfg.Bind("EnabledMods", guid, true, "");

        internal static bool IsEnabled(string guid) => GetEntry(guid).Value;

        internal static void Sync(IEnumerable<RegisteredMod> allMods) {
            // Config entries for all managed mods, including game modes.
            // For game modes, Value=true means "willing to use" (offer to enable on join);
            // Value=false means "opted out" (block join even if the host requires it).
            var managed = allMods.Where(m => m.IsManaged).ToList();
            var live = new HashSet<string>(managed.Select(m => m.Guid));

            // Remove entries for managed mods that are no longer installed
            foreach (var key in _cfg.Keys
                         .Where(k => k.Section == "EnabledMods" && !live.Contains(k.Key))
                         .ToList()) {
                _cfg.Remove(key);
            }

            // Ensure every managed mod has an entry (creates with default=true if absent)
            foreach (var mod in managed) {
                GetEntry(mod.Guid);
            }

            _cfg.Save();
        }

        // ── Active game mode ───────────────────────────────────────────────

        private static ConfigEntry<string> ActiveGameModeEntry =>
            _activeGameMode ??= _cfg.Bind("GameMode", "Active", "",
                "Variant ID of the active game mode. Empty string = Normal (no game mode).");

        /// <summary>Persisted active game mode variant ID. Empty = Normal.</summary>
        internal static string ActiveGameModeId {
            get => ActiveGameModeEntry.Value;
            set {
                ActiveGameModeEntry.Value = value ?? "";
                _cfg.Save();
            }
        }
    }
}
