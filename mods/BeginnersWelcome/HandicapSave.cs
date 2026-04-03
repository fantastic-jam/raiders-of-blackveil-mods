using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace BeginnersWelcome {
    public static class HandicapSave {
        private static readonly string SavePath = Path.Combine(Paths.ConfigPath, "BeginnersWelcome.json");
        private static readonly DataContractJsonSerializerSettings SerializerSettings =
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
        private static readonly DataContractJsonSerializer Serializer =
            new DataContractJsonSerializer(typeof(Dictionary<string, int>), SerializerSettings);

        public static void Load() {
            if (!File.Exists(SavePath))
                return;

            try {
                using var stream = File.OpenRead(SavePath);
                var data = Serializer.ReadObject(stream) as Dictionary<string, int>;
                if (data == null)
                    return;
                HandicapState.Values.Clear();
                foreach (var kv in data)
                    HandicapState.Values[kv.Key] = kv.Value;
            }
            catch (Exception ex) {
                BeginnersWelcomeMod.PublicLogger.LogWarning($"BeginnersWelcome: Failed to load save: {ex.Message}");
            }
        }

        public static void Save() {
            try {
                var nonZero = new Dictionary<string, int>();
                foreach (var kv in HandicapState.Values) {
                    if (kv.Value != 0) {
                        nonZero[kv.Key] = kv.Value;
                    }
                }
                using var stream = File.Create(SavePath);
                Serializer.WriteObject(stream, nonZero);
            }
            catch (Exception ex) {
                BeginnersWelcomeMod.PublicLogger.LogWarning($"BeginnersWelcome: Failed to write save: {ex.Message}");
            }
        }
    }
}
