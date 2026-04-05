using System.Reflection;
using HarmonyLib;
using RR;

namespace OfflineMode {
    public static class OfflineModeState {
        public static bool IsOffline { get; internal set; }

        private static readonly MethodInfo _stateGetter = AccessTools.PropertyGetter(typeof(BackendManager), "State");

        internal static bool IsLoggedIn() {
            if (BackendManager.Instance == null || _stateGetter == null) {
                return false;
            }

            var state = (BackendManager.BackendManagerState)_stateGetter.Invoke(BackendManager.Instance, null);
            return state >= BackendManager.BackendManagerState.ActiveGameSession;
        }
    }
}
