using UnityEngine.UIElements;

namespace ModRegistry {
    /// <summary>
    /// Optional interface for mods that want to expose a settings panel inside the
    /// ModManager Mods menu. Implement alongside <see cref="IModRegistrant"/>, or
    /// expose the same public members by name (duck typing — no DLL reference required).
    /// </summary>
    public interface IModMenuProvider {
        /// <summary>
        /// The name shown as the left-bar section title in the Mods menu.
        /// A non-null value also signals that this mod exposes a settings panel.
        /// </summary>
        public string MenuName { get; }

        /// <summary>
        /// Called when the player selects this mod in the Mods menu left bar.
        /// Add settings controls to <paramref name="container"/>; ModManager owns the container's lifetime.
        /// </summary>
        public void OpenMenu(VisualElement container, bool isInGameMenu);

        /// <summary>
        /// Called when the player navigates away from this mod's settings page or closes the Mods menu.
        /// Use this to persist any unsaved state.
        /// </summary>
        public void CloseMenu();
    }
}
