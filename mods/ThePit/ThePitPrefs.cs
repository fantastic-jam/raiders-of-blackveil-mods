using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using BepInEx;

namespace ThePit {
    [DataContract]
    internal sealed class ThePitPrefs {
        // Null means "not set — use the hard-coded default in HostConfigOverlay".
        // EmitDefaultValue = false means null fields are omitted from the JSON.
        [DataMember(EmitDefaultValue = false)] internal float? DurationSeconds { get; set; }
        [DataMember(EmitDefaultValue = false)] internal float? DropRateMultiplier { get; set; }
        [DataMember(EmitDefaultValue = false)] internal int? InitialPerksCount { get; set; }
        [DataMember(EmitDefaultValue = false)] internal float? DamageReductionFactor { get; set; }
        [DataMember(EmitDefaultValue = false)] internal int? InitialLevel { get; set; }

        private static string PrefsPath =>
            Path.Combine(Paths.ConfigPath, ThePitMod.Id, "prefs.json");

        internal static ThePitPrefs Load() {
            try {
                var path = PrefsPath;
                if (!File.Exists(path)) { return new ThePitPrefs(); }
                using var stream = File.OpenRead(path);
                var ser = new DataContractJsonSerializer(typeof(ThePitPrefs));
                var prefs = (ThePitPrefs)ser.ReadObject(stream) ?? new ThePitPrefs();
                prefs.Sanitize();
                return prefs;
            }
            catch (Exception) {
                return new ThePitPrefs();
            }
        }

        // Reset invalid values to null so the overlay falls back to its hard-coded defaults.
        private void Sanitize() {
            if (DurationSeconds.HasValue && (!float.IsFinite(DurationSeconds.Value) || DurationSeconds.Value <= 0f)) {
                DurationSeconds = null;
            }

            if (DropRateMultiplier.HasValue && (!float.IsFinite(DropRateMultiplier.Value) || DropRateMultiplier.Value <= 0f)) {
                DropRateMultiplier = null;
            }

            if (DamageReductionFactor.HasValue && (!float.IsFinite(DamageReductionFactor.Value) || DamageReductionFactor.Value < 1f)) {
                DamageReductionFactor = null;
            }

            if (InitialPerksCount.HasValue && InitialPerksCount.Value < 1) {
                InitialPerksCount = null;
            }

            if (InitialLevel.HasValue) {
                InitialLevel = Math.Clamp(InitialLevel.Value, 1, 20);
            }
        }

        internal void Save() {
            // Null out default values so they are omitted from the JSON (EmitDefaultValue = false).
            // Defaults: DurationSeconds=600, DropRateMultiplier=1, InitialPerksCount=6,
            //           DamageReductionFactor=20, InitialLevel=5.
            if (DurationSeconds.HasValue && Math.Abs(DurationSeconds.Value - 600f) < 1e-4f) {
                DurationSeconds = null;
            }

            if (DropRateMultiplier.HasValue && Math.Abs(DropRateMultiplier.Value - 1.0f) < 1e-4f) {
                DropRateMultiplier = null;
            }

            if (DamageReductionFactor.HasValue && Math.Abs(DamageReductionFactor.Value - 20f) < 1e-4f) {
                DamageReductionFactor = null;
            }

            if (InitialPerksCount.HasValue && InitialPerksCount.Value == 6) {
                InitialPerksCount = null;
            }

            if (InitialLevel.HasValue && InitialLevel.Value == 5) {
                InitialLevel = null;
            }

            try {
                var path = PrefsPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var stream = File.Create(path);
                var ser = new DataContractJsonSerializer(typeof(ThePitPrefs));
                ser.WriteObject(stream, this);
            }
            catch (Exception ex) {
                ThePitMod.PublicLogger?.LogWarning($"ThePit: failed to save prefs: {ex.Message}");
            }
        }
    }
}
