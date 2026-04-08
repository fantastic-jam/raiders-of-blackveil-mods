using System;

namespace WildguardModFramework.Registry {
    /// <summary>
    /// A single selectable game mode entry as seen by WMF.
    /// One mod can contribute multiple entries (via IGameModeProvider).
    /// Only one RegisteredGameMode can be active per session.
    /// </summary>
    internal sealed class RegisteredGameMode {
        /// <summary>
        /// Globally unique variant ID.
        /// Single-variant mods use the plugin GUID; multi-variant mods use "pluginGuid::variantId".
        /// </summary>
        internal string VariantId { get; }

        /// <summary>Human-readable name shown in the game mode stepper.</summary>
        internal string DisplayName { get; }

        internal string Description { get; }

        /// <summary>GUID of the BepInEx plugin that provides this game mode.</summary>
        internal string PluginGuid { get; }

        /// <summary>
        /// When true, clients joining a session with this game mode active must also have it installed and enabled.
        /// </summary>
        internal bool IsClientRequired { get; }

        /// <summary>
        /// Shown to modded clients once per game launch when they join a session running this game mode.
        /// Unmodded clients are kicked before seeing it. Null means no join message is sent.
        /// </summary>
        internal string JoinMessage { get; }

        /// <summary>
        /// Shown as a HUD corner notification on the first level of a run.
        /// Null means no notification is shown.
        /// </summary>
        internal string RunStartMessage { get; }

        private readonly Action _enable;
        private readonly Action _disable;

        internal RegisteredGameMode(string variantId, string displayName, string description,
                                    string pluginGuid, Action enable, Action disable,
                                    bool isClientRequired = false,
                                    string joinMessage = null,
                                    string runStartMessage = null) {
            VariantId = variantId;
            DisplayName = displayName;
            Description = description;
            PluginGuid = pluginGuid;
            IsClientRequired = isClientRequired;
            JoinMessage = joinMessage;
            RunStartMessage = runStartMessage;
            _enable = enable;
            _disable = disable;
        }

        internal void Enable() => _enable?.Invoke();
        internal void Disable() => _disable?.Invoke();
    }
}
