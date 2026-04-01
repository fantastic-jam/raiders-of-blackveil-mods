using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using BepInEx.Logging;
using RR;

namespace PerfectDodge.Localization {
    public static class PerfectDodgeLocalization {
        private const string DefaultLocale = "en";
        private const string FilePrefix = "PerfectDodge.";

        private static readonly Dictionary<string, Dictionary<string, string>> _translations =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private static ManualLogSource _logger;

        public const string DodgedLabelKey = "perfect_dodge.dodged_label";

        public static void Initialize(ManualLogSource logger) {
            _logger = logger;
            _translations.Clear();

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                 ?? AppDomain.CurrentDomain.BaseDirectory;
            string dir = Path.Combine(assemblyDir, "Assets", "Localization");

            var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>), settings);

            if (!Directory.Exists(dir)) {
                _logger.LogWarning($"PerfectDodge: Localization directory not found at {dir}.");
                return;
            }

            foreach (string filePath in Directory.GetFiles(dir, FilePrefix + "*.json", SearchOption.TopDirectoryOnly)) {
                string name = Path.GetFileNameWithoutExtension(filePath);
                if (name.Length <= FilePrefix.Length) {
                    continue;
                }

                string locale = name.Substring(FilePrefix.Length).ToLowerInvariant();

                try {
                    using (var stream = File.OpenRead(filePath)) {
                        var entries = serializer.ReadObject(stream) as Dictionary<string, string>;
                        _translations[locale] = entries ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch (Exception ex) {
                    _logger.LogWarning($"PerfectDodge: Failed to load '{Path.GetFileName(filePath)}': {ex.Message}");
                }
            }

            if (!_translations.ContainsKey(DefaultLocale)) {
                _logger.LogWarning("PerfectDodge: Missing default English translation file (PerfectDodge.en.json).");
            }
        }

        public static string Get(string key) {
            string code = (AppManager.Instance?.PlayerSettings?.Gen_Language ?? DefaultLocale)
                .Trim().ToLowerInvariant();

            foreach (string locale in Fallbacks(code)) {
                if (_translations.TryGetValue(locale, out var table)
                    && table.TryGetValue(key, out string value)
                    && !string.IsNullOrEmpty(value)) {
                    return value;
                }
            }

            return key;
        }

        private static IEnumerable<string> Fallbacks(string code) {
            yield return code;

            int sep = code.IndexOf('-');
            if (sep > 0) {
                yield return code.Substring(0, sep);
            }

            if (!string.Equals(code, DefaultLocale, StringComparison.OrdinalIgnoreCase)) {
                yield return DefaultLocale;
            }
        }
    }
}
