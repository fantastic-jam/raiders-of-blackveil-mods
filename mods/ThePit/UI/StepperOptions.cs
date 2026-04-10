using System;
using System.Globalization;

namespace ThePit.UI {
    // Parses BepInEx config strings into stepper option arrays.
    // On any parse error, the supplied defaults are returned unchanged.
    internal static class StepperOptions {
        // Parses "Label:float,Label:float,..." — colons inside labels are not supported.
        internal static (string Label, float Value)[] ParseFloat(
                string raw, (string Label, float Value)[] defaults) {
            try {
                var entries = raw.Split(',');
                if (entries.Length == 0) { return defaults; }
                var result = new (string Label, float Value)[entries.Length];
                for (int i = 0; i < entries.Length; i++) {
                    int colon = entries[i].IndexOf(':');
                    if (colon < 0) { return defaults; }
                    string label = entries[i][..colon].Trim();
                    float value = float.Parse(entries[i][(colon + 1)..].Trim(), CultureInfo.InvariantCulture);
                    result[i] = (label, value);
                }
                return result;
            }
            catch { return defaults; }
        }

        // Parses "Label:int,Label:int,..." or bare "int,int,..." (label auto-generated from value).
        internal static (string Label, int Value)[] ParseInt(
                string raw, (string Label, int Value)[] defaults) {
            try {
                var entries = raw.Split(',');
                if (entries.Length == 0) { return defaults; }
                var result = new (string Label, int Value)[entries.Length];
                for (int i = 0; i < entries.Length; i++) {
                    int colon = entries[i].IndexOf(':');
                    if (colon >= 0) {
                        string label = entries[i][..colon].Trim();
                        int value = int.Parse(entries[i][(colon + 1)..].Trim(), CultureInfo.InvariantCulture);
                        result[i] = (label, value);
                    } else {
                        int value = int.Parse(entries[i].Trim(), CultureInfo.InvariantCulture);
                        result[i] = (value.ToString(), value);
                    }
                }
                return result;
            }
            catch { return defaults; }
        }

        // Clamps a saved index to the bounds of a (possibly re-parsed) options array.
        internal static int Clamp(int savedIndex, int arrayLength) =>
            Math.Max(0, Math.Min(savedIndex, arrayLength - 1));
    }
}
