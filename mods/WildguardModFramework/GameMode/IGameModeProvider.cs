using System.Collections.Generic;

namespace WildguardModFramework {
    /// <summary>
    /// Optional interface for <see cref="ModRegistry.ModType.GameMode"/> mods that expose multiple
    /// selectable variants (e.g. "Rogue Run" and "Rogue Run — Hard" from the same plugin).
    /// Implement alongside <see cref="ModRegistry.IModRegistrant"/> and add
    /// <c>[BepInDependency(WmfMod.Id)]</c> so BepInEx guarantees WMF is loaded first.
    /// <para>
    /// WMF registers one stepper entry per <see cref="GameModeVariants"/> item.
    /// <see cref="ModRegistry.IModRegistrant.Enable"/> is called first, then
    /// <see cref="EnableVariant"/> with the chosen variant ID.
    /// <see cref="ModRegistry.IModRegistrant.Disable"/> deactivates all variants.
    /// </para>
    /// </summary>
    public interface IGameModeProvider {
        /// <summary>All variants this mod exposes as selectable game modes.</summary>
        public IReadOnlyList<GameModeVariant> GameModeVariants { get; }

        /// <summary>
        /// Called after <see cref="ModRegistry.IModRegistrant.Enable"/> to activate a specific variant.
        /// Only one variant is active at a time.
        /// </summary>
        public void EnableVariant(string variantId);
    }
}
