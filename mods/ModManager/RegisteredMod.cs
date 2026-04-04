using System;
using ModRegistry;

namespace ModManager {
    internal sealed class RegisteredMod {
        internal ModType Type { get; }
        internal string Name { get; }
        internal string Description { get; }

        private readonly Action _disable;

        internal bool Disabled { get; private set; }

        internal RegisteredMod(ModType type, string name, string description, Action disable) {
            Type = type;
            Name = name;
            Description = description;
            _disable = disable;
        }

        internal void Disable() {
            if (Disabled) { return; }
            Disabled = true;
            _disable();
        }
    }
}
