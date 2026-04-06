namespace ModRegistry {
    public enum ModType {
        Mod,
        Cosmetics,
        Cheat,
        Utility,
        /// <summary>
        /// Mutually exclusive with all other game modes. Only one GameMode can be active per session.
        /// Disabled by default; enabled via the Game Mode selector in the host/solo UI.
        /// </summary>
        GameMode
    }
}
