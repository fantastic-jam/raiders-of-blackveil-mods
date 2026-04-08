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

        /// <summary>
        /// Shown to clients when they join a session running this variant.
        /// Modded clients see it once per game launch; unmodded clients see it every time someone joins.
        /// Null means no join message is sent.
        /// </summary>
        public string JoinMessage { get; }

        /// <summary>
        /// Shown as a HUD corner notification on the first level of a run.
        /// Null means no notification is shown.
        /// </summary>
        public string RunStartMessage { get; }

        public GameModeVariant(string variantId, string displayName, string description = "",
                               string joinMessage = null, string runStartMessage = null) {
            VariantId = variantId;
            DisplayName = displayName;
            Description = description ?? "";
            JoinMessage = joinMessage;
            RunStartMessage = runStartMessage;
        }
    }
}
