using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx;
using RR;

namespace WildguardModFramework.Translation {
    /// <summary>
    /// Loads and resolves mod translations from flat JSON files.
    ///
    /// File locations:
    ///   - Own translations:       {pluginDir}/Assets/Localization/{modName}.{lang}.json
    ///   - Third-party overrides:  {pluginsRoot}/*/Translations/{modName}.{lang}.json
    ///
    /// Resolution order: current language → "en" → key itself.
    /// Third-party files are merged after own files; last file wins per key.
    /// </summary>
    public static class TranslationService {
        // modName → lang → key → value
        private static readonly Dictionary<string, Dictionary<string, Dictionary<string, string>>> _registry = new();

        private static readonly DataContractJsonSerializerSettings _jsonSettings =
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };

        private static readonly DataContractJsonSerializer _serializer =
            new DataContractJsonSerializer(typeof(Dictionary<string, string>), _jsonSettings);

        /// <summary>
        /// Returns a <see cref="T"/> delegate bound to the given mod.
        /// Call once in Awake() and store as <c>internal static T t</c>.
        /// </summary>
        public static T For(string modName, string dllPath) {
            Load(modName, dllPath);
            return (key, args) => Resolve(modName, key, args);
        }

        private static void Load(string modName, string dllPath) {
            if (!_registry.ContainsKey(modName)) {
                _registry[modName] = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            }

            var pluginDir = Path.GetDirectoryName(dllPath);
            if (pluginDir != null) {
                LoadFromDir(modName, Path.Combine(pluginDir, "Assets", "Localization"));
            }

            if (!Directory.Exists(Paths.PluginPath)) { return; }

            foreach (var dir in Directory.GetDirectories(Paths.PluginPath)) {
                LoadFromDir(modName, Path.Combine(dir, "Translations"));
            }
        }

        private static void LoadFromDir(string modName, string dir) {
            if (!Directory.Exists(dir)) { return; }

            foreach (var file in Directory.GetFiles(dir, $"{modName}.*.json")) {
                var stem = Path.GetFileNameWithoutExtension(file); // e.g. "ThePit.fr"
                var dot = stem.IndexOf('.');
                if (dot < 0) { continue; }

                var lang = stem.Substring(dot + 1);
                var entries = ParseJson(file);
                if (entries == null) { continue; }

                if (!_registry[modName].TryGetValue(lang, out var existing)) {
                    _registry[modName][lang] = entries;
                } else {
                    foreach (var kv in entries) {
                        existing[kv.Key] = kv.Value;
                    }
                }

                WmfMod.PublicLogger?.LogDebug($"[Translation] Loaded {Path.GetFileName(file)} ({entries.Count} keys)");
            }
        }

        private static Dictionary<string, string> ParseJson(string path) {
            try {
                using var stream = File.OpenRead(path);
                return (Dictionary<string, string>)_serializer.ReadObject(stream);
            }
            catch (Exception ex) {
                WmfMod.PublicLogger?.LogWarning($"[Translation] Failed to parse {path}: {ex.Message}");
                return null;
            }
        }

        private static string Resolve(string modName, string key, (string Key, object Value)[] args) {
            var lang = GetCurrentLang();
            var value = Lookup(modName, lang, key)
                ?? (lang != "en" ? Lookup(modName, "en", key) : null)
                ?? key;

            if (args == null || args.Length == 0) { return value; }

            var sb = new StringBuilder(value);
            foreach (var (k, v) in args) {
                sb.Replace($"{{{k}}}", v?.ToString() ?? string.Empty);
            }
            return sb.ToString();
        }

        private static string Lookup(string modName, string lang, string key) {
            if (!_registry.TryGetValue(modName, out var langs)) { return null; }
            if (!langs.TryGetValue(lang, out var keys)) { return null; }
            return keys.TryGetValue(key, out var val) ? val : null;
        }

        private static string GetCurrentLang() {
            try {
                return AppManager.Instance?.PlayerSettings?.Gen_Language ?? "en";
            }
            catch {
                return "en";
            }
        }
    }
}
