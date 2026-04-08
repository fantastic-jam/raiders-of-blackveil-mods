using System.Linq;
using WildguardModFramework.Registry;
using RR;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Stateless session-start orchestration: applies mod/cheat enable-disable choices,
    /// activates the selected game mode, and computes + appends the session name suffix.
    /// HostStartPagePatch.BeginPlaySessionPrefix delegates here; the ref manipulation
    /// stays in the patch because ref parameters cannot cross lambda or async boundaries.
    /// </summary>
    internal static class SessionOrchestrator {
        // Maximum session name length enforced by the backend.
        internal const int SessionNameMaxLength = 31;

        /// <summary>
        /// Apply all session-start logic and modify sessionTag in-place with the computed suffix.
        /// </summary>
        internal static void Begin(ref string sessionTag, bool allowMods, bool allowCheats, BackendManager.PlaySessionMode playSessionMode) {
            WmfMod.PublicLogger.LogInfo(
                $"WMF: BeginPlaySession — mode={playSessionMode}, allowMods={allowMods}, allowCheats={allowCheats}"
            );

            ApplyModChoices(allowMods, allowCheats);
            var activeGameMode = ApplyGameModeChoice(playSessionMode);

            bool activeCheats = allowCheats && ModScanner.Cheats.Any(m => WmfConfig.IsEnabled(m.Guid));
            bool activeMods = allowMods && ModScanner.Mods.Any(m => WmfConfig.IsEnabled(m.Guid));

            var suffix = ComputeSuffix(activeCheats, activeMods, activeGameMode);
            if (string.IsNullOrEmpty(suffix)) { return; }

            int maxBase = SessionNameMaxLength - suffix.Length;
            if (maxBase < 0) { maxBase = 0; }
            if (sessionTag.Length > maxBase) { sessionTag = sessionTag.Substring(0, maxBase); }
            sessionTag += suffix;
        }

        private static void ApplyModChoices(bool allowMods, bool allowCheats) {
            foreach (var mod in ModScanner.Cheats.Where(m => WmfConfig.IsEnabled(m.Guid))) {
                if (allowCheats) {
                    WmfMod.PublicLogger.LogInfo($"WMF: enabling cheat — {mod.Name}");
                    mod.Enable();
                } else {
                    WmfMod.PublicLogger.LogInfo($"WMF: disabling cheat — {mod.Name}");
                    mod.Disable();
                }
            }
            foreach (var mod in ModScanner.Mods.Where(m => WmfConfig.IsEnabled(m.Guid))) {
                if (allowMods) {
                    WmfMod.PublicLogger.LogInfo($"WMF: enabling mod — {mod.Name}");
                    mod.Enable();
                } else {
                    WmfMod.PublicLogger.LogInfo($"WMF: disabling mod — {mod.Name}");
                    mod.Disable();
                }
            }
        }

        // Game mode activation for host sessions only.
        // Solo game modes are activated by SoloModePickerInjector.ConfirmAndStart() before this fires.
        private static RegisteredGameMode ApplyGameModeChoice(BackendManager.PlaySessionMode playSessionMode) {
            if (playSessionMode == BackendManager.PlaySessionMode.SinglePlayer) { return null; }

            var selectedId = ModScanner.SelectedGameModeVariantId;
            foreach (var gm in ModScanner.GameModes) { gm.Disable(); }

            RegisteredGameMode activeGameMode = null;
            if (selectedId != null) {
                activeGameMode = ModScanner.GameModes.FirstOrDefault(g => g.VariantId == selectedId);
                if (activeGameMode != null) {
                    WmfMod.PublicLogger.LogInfo($"WMF: enabling game mode — {activeGameMode.DisplayName}");
                    activeGameMode.Enable();
                }
            }

            WmfConfig.ActiveGameModeId = selectedId ?? "";
            return activeGameMode;
        }

        private static string ComputeSuffix(bool activeCheats, bool activeMods, RegisteredGameMode activeGameMode) {
            string suffix = activeCheats ? " (cheats)" : activeMods && activeGameMode == null ? " (modded)" : "";
            if (activeGameMode != null) { suffix += " [" + activeGameMode.DisplayName + "]"; }
            return suffix;
        }
    }
}
