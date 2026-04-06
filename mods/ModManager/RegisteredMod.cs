using System;
using ModRegistry;
using UnityEngine.UIElements;

namespace ModManager {
    internal sealed class RegisteredMod {
        internal ModType Type { get; }
        internal string Guid { get; }
        internal string Name { get; }
        internal string Description { get; }

        /// <summary>True when this mod exposes Disable() — either via IModRegistrant or duck typing.</summary>
        internal bool IsManaged { get; }

        /// <summary>
        /// Section title shown in the Mods menu left bar, or null if this mod has no settings panel.
        /// </summary>
        internal string MenuName { get; }

        private readonly Action _disable;
        private readonly Action _enable;
        private readonly Action<VisualElement, bool> _openMenu;
        private readonly Action _closeMenu;

        internal bool Disabled { get; private set; }

        /// <summary>Managed mod — has enable/disable support.</summary>
        internal RegisteredMod(ModType type, string guid, string name, string description,
                               Action disable, Action enable = null,
                               string menuName = null,
                               Action<VisualElement, bool> openMenu = null, Action closeMenu = null) {
            Type = type;
            Guid = guid;
            Name = name;
            Description = description;
            IsManaged = true;
            MenuName = menuName;
            _disable = disable;
            _enable = enable ?? (() => { });
            _openMenu = openMenu;
            _closeMenu = closeMenu;
        }

        /// <summary>Unmanaged mod — listed in the UI but cannot be toggled.</summary>
        internal RegisteredMod(string guid, string name) {
            Type = ModType.Utility;
            Guid = guid;
            Name = name;
            Description = "";
            IsManaged = false;
            _disable = () => { };
            _enable = () => { };
        }

        internal void Disable() {
            if (!IsManaged || Disabled) { return; }
            Disabled = true;
            _disable();
        }

        internal void Enable() {
            if (!IsManaged) { return; }
            Disabled = false;
            _enable();
        }

        internal void OpenMenu(VisualElement container, bool isInGameMenu) => _openMenu?.Invoke(container, isInGameMenu);
        internal void CloseMenu() => _closeMenu?.Invoke();
    }
}
