using HarmonyLib;

namespace GhoulagUrskov.Patch {
    internal static class GhoulagUrskovPatch {
        private static Harmony _harmony;
        private static bool _patched;
        internal static bool Disabled;

        internal static bool Init() {
            return true;
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) {
                return;
            }
            _harmony = harmony;
            GhoulagUrskovMod.PublicLogger.LogInfo("GhoulagUrskov patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }
    }
}
