using System;
using System.Collections.Generic;

namespace BeginnersWelcome {
    public static class HandicapState {
        private const float MaxMultiplier = 5f;

        public static readonly Dictionary<int, int> Values = new();

        public static float Multiplier(int slotIndex) {
            Values.TryGetValue(slotIndex, out var handicap);
            return (float)Math.Pow(MaxMultiplier, handicap / 10f);
        }
    }
}
