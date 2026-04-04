using System;
using System.Collections.Generic;

namespace BeginnersWelcome {
    public static class HandicapState {
        private const float MaxMultiplier = 5f;

        // Keyed by player ProfileUUID
        public static readonly Dictionary<string, int> Values = new();

        public static float Multiplier(string uuid) {
            Values.TryGetValue(uuid, out var handicap);
            return (float)Math.Pow(MaxMultiplier, handicap / 10f);
        }
    }
}
