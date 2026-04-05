using System;
using ModRegistry;

namespace ModManager {
    internal sealed class RegisteredMod {
        internal ModType Type { get; }
        internal string Name { get; }
        internal string Description { get; }

        private readonly Action _disable;
        private readonly Action _enable;

        internal bool Disabled { get; private set; }

        internal RegisteredMod(ModType type, string name, string description, Action disable, Action enable = null) {
            Type = type;
            Name = name;
            Description = description;
            _disable = disable;
            _enable = enable ?? (() => { });
        }

        internal void Disable() {
            if (Disabled) { return; }
            Disabled = true;
            _disable();
        }

        internal void Enable() {
            Disabled = false;
            _enable();
        }
    }
}
