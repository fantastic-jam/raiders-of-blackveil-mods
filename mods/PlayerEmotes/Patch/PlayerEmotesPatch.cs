using System.Reflection;
using HarmonyLib;
using RR.UI.Pages;

namespace PlayerEmotes.Patch {
    internal static class PlayerEmotesPatch {
        private static Harmony _harmony;
        private static bool _patched;
        private static MethodInfo _hudOnUpdate;

        internal static bool Init() {
            _hudOnUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (_hudOnUpdate == null) {
                PlayerEmotesMod.PublicLogger.LogWarning("PlayerEmotes: BaseHUDPage.OnUpdate not found — emote key unavailable.");
            }
            return true;
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;
            if (_hudOnUpdate != null) {
                _harmony.Patch(_hudOnUpdate, postfix: new HarmonyMethod(typeof(PlayerEmotesPatch), nameof(OnHUDUpdatePostfix)));
            }
            PlayerEmotesMod.PublicLogger.LogInfo("PlayerEmotes patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }

        private static void OnHUDUpdatePostfix() {
            EmoteInput.OnUpdate();
        }
    }
}
