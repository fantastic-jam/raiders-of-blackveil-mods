namespace ModRegistry {
    /// <summary>
    /// Optional interface for BepInEx plugin classes to participate in ModManager.
    /// Alternatively, mods can follow the same method-name convention without
    /// referencing this DLL — ModManager discovers them via duck typing.
    ///
    /// Only <see cref="GetModType"/> and <see cref="Disable"/> are required.
    /// <see cref="GetModName"/> and <see cref="GetModDescription"/> are optional:
    /// if absent or empty, ModManager falls back to the BepInEx plugin name / empty string.
    /// </summary>
    public interface IModRegistrant {
        /// <summary>Returns "Mod", "Cheat", or "Cosmetics".</summary>
        public string GetModType();

        /// <summary>
        /// Undo this mod's effects using the noop pattern: set a static <c>Disabled</c> flag
        /// on the patch class so all postfix/prefix methods short-circuit.
        /// Called at most once per game session, just before <c>BeginPlaySession</c>.
        /// </summary>
        public void Disable();

        /// <summary>
        /// Returns <c>true</c> after <see cref="Disable"/> has been called.
        /// Implement by forwarding to a static <c>Disabled</c> property on the patch class.
        /// </summary>
        public bool Disabled { get; }

        /// <summary>Human-readable mod name. Empty → ModManager uses the BepInEx plugin name.</summary>
        public string GetModName() => "";

        /// <summary>Short description shown in ModManager UI. Empty is fine.</summary>
        public string GetModDescription() => "";
    }
}
