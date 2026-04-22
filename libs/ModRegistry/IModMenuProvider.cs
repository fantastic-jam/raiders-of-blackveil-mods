using System;
using UnityEngine.UIElements;

namespace ModRegistry {
    /// <summary>
    /// Optional interface for mods that want to expose a settings panel in the WMF Mods menu.
    /// Alternatively, expose the same public members by name without referencing this DLL (duck typing).
    /// All members are required when implementing this interface.
    /// </summary>
    public interface IModMenuProvider {
        /// <summary>Section title shown in the Mods menu left bar.</summary>
        public string MenuName { get; }

        /// <summary>
        /// Called when the player selects this mod in the Mods menu left bar.
        /// Add settings controls to <paramref name="container"/>.
        /// </summary>
        public void OpenMenu(VisualElement container, bool isInGameMenu);

        /// <summary>Called when the player navigates away. Use to persist unsaved state.</summary>
        public void CloseMenu();

        /// <summary>
        /// Optional sub-sections shown as an expandable accordion under MenuName.
        /// Return an empty array or null if not used.
        /// </summary>
        public (string Title, Action<VisualElement, bool> Build)[] SubMenus { get; }
    }
}
