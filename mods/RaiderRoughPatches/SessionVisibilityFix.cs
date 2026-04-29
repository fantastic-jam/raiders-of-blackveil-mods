using System.Reflection;
using HarmonyLib;
using RR;

namespace RaiderRoughPatches {
    // Base-game bug: after returning from a run, ConfirmPlaySessionStartupRegion is not re-called,
    // so the session loses its Fusion region binding and disappears from the server list.
    internal static class SessionVisibilityFix {
        private static FieldInfo _currentRegionField;
        private static MethodInfo _confirmStartupRegionMethod;

        internal static void Init() {
            _currentRegionField = AccessTools.Field(typeof(NetworkManager), "CurrentRegion");
            if (_currentRegionField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: NetworkManager.CurrentRegion not found — session visibility fix inactive.");
            }
            _confirmStartupRegionMethod = AccessTools.Method(typeof(BackendManager), "ConfirmPlaySessionStartupRegion");
            if (_confirmStartupRegionMethod == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: BackendManager.ConfirmPlaySessionStartupRegion not found — session visibility fix inactive.");
            }
        }

        internal static void OnLobbySceneLoadDone() {
            var bm = BackendManager.Instance;
            if (bm == null || _currentRegionField == null || _confirmStartupRegionMethod == null) { return; }
            if (PlayerManager.Instance?.Runner?.IsServer != true) { return; }
            var regionBox = _currentRegionField.GetValue(null);
            if (regionBox is not System.ValueTuple<string, int> region || string.IsNullOrEmpty(region.Item1) || region.Item1 == "<region-not-set>") {
                RaiderRoughPatchesMod.PublicLogger.LogWarning("RaiderRoughPatches: Fusion region not available — skipping region re-confirm.");
                return;
            }
            RaiderRoughPatchesMod.PublicLogger.LogInfo($"RaiderRoughPatches: re-confirming startup region={region.Item1}.");
            _confirmStartupRegionMethod.Invoke(bm, new object[] { regionBox });
        }
    }
}
