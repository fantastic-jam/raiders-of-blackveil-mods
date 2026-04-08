using System.Linq;
using WildguardModFramework.Registry;

namespace WildguardModFramework.Lifecycle {
    internal static class ModLifecycle {
        internal static void DisableAllGameModes() {
            foreach (var gm in ModScanner.GameModes) {
                gm.Disable();
            }
        }

        internal static void ApplyStartupDisables() {
            // Regular mods: use per-mod enabled config (skip game mode mods — handled separately)
            foreach (var mod in ModScanner.AllDiscovered.Where(m => m.IsManaged && m.Type != ModRegistry.ModType.GameMode)) {
                if (!WmfConfig.IsEnabled(mod.Guid)) {
                    WmfMod.PublicLogger.LogInfo($"WMF: startup disable — {mod.Name}");
                    mod.Disable();
                }
            }

            // Game modes: disable all, then enable the active variant
            foreach (var gm in ModScanner.GameModes) {
                gm.Disable();
            }

            if (ModScanner.SelectedGameModeVariantId != null) {
                var active = ModScanner.GameModes.FirstOrDefault(g => g.VariantId == ModScanner.SelectedGameModeVariantId);
                if (active != null) {
                    WmfMod.PublicLogger.LogInfo($"WMF: startup enable game mode — {active.DisplayName}");
                    active.Enable();
                }
            }
        }
    }
}
