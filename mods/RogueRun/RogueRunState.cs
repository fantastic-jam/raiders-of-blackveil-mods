using System.Collections.Generic;
using RR.Game;

namespace RogueRun {
    public static class RogueRunState {
        /// <summary>True when RogueRun is the selected game mode for this session.</summary>
        public static bool IsActive { get; internal set; }

        /// <summary>True only while inside a dungeon level (between EventBeginLevel and lobby return).</summary>
        internal static bool InRun { get; set; }

        /// <summary>
        /// Champion inventory snapshot taken at dungeon entry, keyed by player slot index.
        /// Restored by the server when returning to lobby; discarded after restore.
        /// </summary>
        internal static readonly Dictionary<int, InventoryChampionBackendData> PreRunSnapshot = new();

        internal static void ClearSnapshot() => PreRunSnapshot.Clear();
    }
}
