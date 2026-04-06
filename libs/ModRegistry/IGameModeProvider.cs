using System.Collections.Generic;

namespace ModRegistry {
    /// <summary>
    /// Optional interface for <see cref="ModType.GameMode"/> mods that expose multiple selectable variants
    /// (e.g. "Rogue Run" and "Rogue Run — Hard" from the same plugin).
    /// <para>
    /// When a mod implements both <see cref="IModRegistrant"/> and <see cref="IGameModeProvider"/>,
    /// ModManager registers one entry per <see cref="GameModeVariants"/> item instead of one entry for the whole mod.
    /// <see cref="IModRegistrant.Enable"/> is called first, then <see cref="EnableVariant"/> with the chosen variant ID.
    /// <see cref="IModRegistrant.Disable"/> deactivates all variants.
    /// </para>
    /// </summary>
    public interface IGameModeProvider {
        /// <summary>All variants this mod exposes as selectable game modes.</summary>
        public IReadOnlyList<GameModeVariant> GameModeVariants { get; }

        /// <summary>
        /// Called after <see cref="IModRegistrant.Enable"/> to activate a specific variant.
        /// Only one variant is active at a time.
        /// </summary>
        public void EnableVariant(string variantId);
    }
}
