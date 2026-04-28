using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace WildguardModFramework.PlayerManagement {
    internal static class BanList {
        private sealed class Entry {
            internal Guid Id { get; }
            internal string DisplayName { get; }
            internal Entry(Guid id, string displayName) { Id = id; DisplayName = displayName; }
        }

        private static ConfigEntry<string> _entry;

        internal static void Init(ConfigFile cfg) {
            _entry = cfg.Bind("PlayerManagement", "BannedPlayers", "",
                "Comma-separated banned players in 'guid:name' format.");
        }

        internal static bool IsBanned(Guid id) => Parse().Any(e => e.Id == id);

        internal static void Add(Guid id, string displayName) {
            var entries = Parse();
            if (entries.Any(e => e.Id == id)) { return; }
            var safeName = (displayName ?? "?").Replace(",", "").Replace(":", "");
            entries.Add(new Entry(id, safeName));
            if (entries.Count > 50) {
                WmfMod.PublicLogger.LogWarning("WMF: ban list exceeds 50 entries — consider reviewing.");
            }
            Save(entries);
        }

        internal static void Remove(Guid id) => Save(Parse().Where(e => e.Id != id).ToList());

        internal static IReadOnlyList<(Guid Id, string DisplayName)> All() =>
            Parse().Select(e => (e.Id, e.DisplayName)).ToList();

        private static List<Entry> Parse() {
            if (_entry == null || string.IsNullOrWhiteSpace(_entry.Value)) { return new List<Entry>(); }
            var entries = new List<Entry>();
            foreach (var part in _entry.Value.Split(',')) {
                var s = part.Trim();
                if (s.Length == 0) { continue; }
                var sep = s.IndexOf(':');
                if (sep < 0) { continue; }
                if (!Guid.TryParse(s.Substring(0, sep), out var g)) { continue; }
                entries.Add(new Entry(g, sep + 1 < s.Length ? s.Substring(sep + 1) : "?"));
            }
            return entries;
        }

        private static void Save(List<Entry> entries) {
            _entry.Value = string.Join(",", entries.Select(e => $"{e.Id}:{e.DisplayName}"));
            _entry.ConfigFile.Save();
        }
    }
}
