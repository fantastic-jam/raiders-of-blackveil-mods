namespace ModRegistry {
    /// <summary>
    /// Optional interface for BepInEx plugin classes to participate in ModManager.
    /// Alternatively, mods can follow the same method-name convention without
    /// referencing this DLL — ModManager discovers them via duck typing.
    ///
    /// Only <see cref="GetModType"/> and <see cref="Disable"/> are required.
    /// <see cref="Enable"/>, <see cref="GetModName"/>, and <see cref="GetModDescription"/> are optional.
    /// </summary>
    public interface IModRegistrant {
        /// <summary>Returns "Mod", "Cheat", or "Cosmetics".</summary>
        public string GetModType();

        /// <summary>
        /// Undo this mod's effects using the noop pattern: set a static <c>Disabled</c> flag
        /// on the patch class so all postfix/prefix methods short-circuit.
        /// Called just before <c>BeginPlaySession</c> when the host disallows this mod type.
        /// </summary>
        public void Disable();

        /// <summary>
        /// Re-enable this mod after a previous <see cref="Disable"/> call.
        /// Implement by clearing the static <c>Disabled</c> flag on the patch class.
        /// Called just before <c>BeginPlaySession</c> when the host allows this mod type.
        /// Default: no-op (safe for mods that are never disabled mid-session).
        /// </summary>
        public void Enable() { }

        /// <summary>
        /// Returns <c>true</c> after <see cref="Disable"/> has been called (and <c>false</c> after <see cref="Enable"/>).
        /// Implement by forwarding to a static <c>Disabled</c> property on the patch class.
        /// </summary>
        public bool Disabled { get; }

        /// <summary>Human-readable mod name. Empty → ModManager uses the BepInEx plugin name.</summary>
        public string GetModName() => "";

        /// <summary>Short description shown in ModManager UI. Empty is fine.</summary>
        public string GetModDescription() => "";

        /// <summary>
        /// When true, clients joining a session with this game mode active must also have it
        /// installed and enabled. ModManager will block the join and offer to enable it if
        /// the client has it installed but disabled. Only meaningful for GameMode mods.
        /// </summary>
        public bool IsClientRequired => false;
    }
}
