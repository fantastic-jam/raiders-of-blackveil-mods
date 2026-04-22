namespace ModRegistry {
    /// <summary>
    /// Optional interface for BepInEx plugin classes to participate in WMF.
    /// Alternatively, implement the same public methods by name without referencing this DLL
    /// (duck typing — WMF discovers them via reflection).
    ///
    /// All members are required when implementing this interface. If you only want to expose
    /// some members, omit the interface and rely on duck typing instead.
    /// </summary>
    public interface IModRegistrant {
        /// <summary>Returns "Mod", "Cheat", "GameMode", "Cosmetics", or "Utility".</summary>
        public string GetModType();

        /// <summary>Human-readable mod name. Empty string falls back to the BepInEx plugin name.</summary>
        public string GetModName();

        /// <summary>Short description shown in the WMF Mods menu. Empty string is fine.</summary>
        public string GetModDescription();

        /// <summary>
        /// Disable this mod's effects. Use the noop pattern: set a static Disabled flag that
        /// all patch methods check. Called before BeginPlaySession when the host disallows this mod type.
        /// </summary>
        public void Disable();

        /// <summary>
        /// Re-enable this mod after a previous Disable() call. Clear the static Disabled flag.
        /// Called before BeginPlaySession when the host allows this mod type.
        /// </summary>
        public void Enable();

        /// <summary>
        /// True after Disable() has been called, false after Enable().
        /// Forward to a static Disabled property on the patch class.
        /// </summary>
        public bool Disabled { get; }

        /// <summary>
        /// When true, clients joining a session with this game mode active must also have it
        /// installed and enabled. Only meaningful for GameMode mods.
        /// </summary>
        public bool IsClientRequired { get; }
    }
}
