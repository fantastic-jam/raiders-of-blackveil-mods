namespace ModRegistry {
    /// <summary>
    /// Describes a single selectable game mode variant exposed by an <see cref="IGameModeProvider"/> mod.
    /// </summary>
    public sealed class GameModeVariant {
        /// <summary>Unique identifier for this variant within the mod (e.g. "normal", "hard").</summary>
        public string VariantId { get; }

        /// <summary>Human-readable name shown in the UI (e.g. "Rogue Run", "Rogue Run — Hard").</summary>
        public string DisplayName { get; }

        /// <summary>Short description shown in tooltips or the Mods overlay.</summary>
        public string Description { get; }

        public GameModeVariant(string variantId, string displayName, string description = "") {
            VariantId = variantId;
            DisplayName = displayName;
            Description = description ?? "";
        }
    }
}
