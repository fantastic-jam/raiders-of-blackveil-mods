using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace ModManager {
    internal static class ModManagerConfig {
        private static ConfigFile _cfg;

        internal static void Init(ConfigFile cfg) => _cfg = cfg;

        internal static ConfigEntry<bool> GetEntry(string guid) =>
            _cfg.Bind("EnabledMods", guid, true, "");

        internal static bool IsEnabled(string guid) => GetEntry(guid).Value;

        internal static void Sync(IEnumerable<RegisteredMod> allMods) {
            // Config entries only exist for managed mods (those that can be disabled)
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
    }
}
