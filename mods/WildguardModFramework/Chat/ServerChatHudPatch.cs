using System.Reflection;
using HarmonyLib;
using RR.Input;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine.InputSystem;

namespace WildguardModFramework.Chat {
    internal static class ServerChatHudPatch {
        private static MethodInfo _isAnyPageOpenGetter;

        internal static void Apply(Harmony harmony) {
            _isAnyPageOpenGetter = AccessTools.Property(typeof(GameHUDPage), "IsAnyPageOpen")
                ?.GetGetMethod(nonPublic: true);
            if (_isAnyPageOpenGetter == null) {
                WmfMod.PublicLogger.LogWarning("WMF: GameHUDPage.IsAnyPageOpen not found — chat may open over sub-pages.");
            }

            var onInit = AccessTools.Method(typeof(BaseHUDPage), "OnInit");
            if (onInit == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BaseHUDPage.OnInit not found — chat overlay inactive.");
            } else {
                harmony.Patch(onInit, postfix: new HarmonyMethod(typeof(ServerChatHudPatch), nameof(OnInitPostfix)));
            }

            var onUpdate = AccessTools.Method(typeof(BaseHUDPage), "OnUpdate");
            if (onUpdate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BaseHUDPage.OnUpdate not found — chat open key inactive.");
            } else {
                harmony.Patch(onUpdate, postfix: new HarmonyMethod(typeof(ServerChatHudPatch), nameof(OnUpdatePostfix)));
            }

            var onDeactivate = AccessTools.Method(typeof(BaseHUDPage), "OnDeactivate");
            if (onDeactivate != null) {
                harmony.Patch(onDeactivate, postfix: new HarmonyMethod(typeof(ServerChatHudPatch), nameof(OnDeactivatePostfix)));
            }

            var navigatePrefix = new HarmonyMethod(typeof(ServerChatHudPatch), nameof(OnNavigateInputPrefix));
            var gameNavInput = AccessTools.Method(typeof(GameHUDPage), "OnNavigateInput");
            if (gameNavInput != null) { harmony.Patch(gameNavInput, prefix: navigatePrefix); }
            var lobbyNavInput = AccessTools.Method(typeof(LobbyHUDPage), "OnNavigateInput");
            if (lobbyNavInput != null) { harmony.Patch(lobbyNavInput, prefix: navigatePrefix); }
        }

        private static void OnInitPostfix(BaseHUDPage __instance) {
            if (__instance is GameHUDPage || __instance is LobbyHUDPage) {
                ServerChat.OnHudInit(__instance);
            }
        }

        private static string _lastPageType;

        private static void OnUpdatePostfix(BaseHUDPage __instance) {
            if (!(__instance is GameHUDPage) && !(__instance is LobbyHUDPage)) { return; }
            var typeName = __instance.GetType().Name;
            if (typeName != _lastPageType) {
                _lastPageType = typeName;
                WmfMod.PublicLogger.LogInfo($"[Chat] OnUpdate page type: {typeName}");
            }
            ServerChat.SetActivePage(__instance);
            if (ServerChat.IsOpen || ServerChat.SuppressOpen) { return; }
            var kb = Keyboard.current;
            if (kb?.enterKey.wasPressedThisFrame != true && kb?.numpadEnterKey.wasPressedThisFrame != true) { return; }
            WmfMod.PublicLogger.LogInfo("[Chat] Enter detected");
            if (__instance is GameHUDPage gameHud) {
                bool anyPageOpen = _isAnyPageOpenGetter != null && (bool)_isAnyPageOpenGetter.Invoke(gameHud, null);
                if (anyPageOpen) { return; }
            }
            ServerChat.Open();
        }

        private static void OnDeactivatePostfix(BaseHUDPage __instance) {
            if (__instance is GameHUDPage || __instance is LobbyHUDPage) {
                ServerChat.ForceClose(__instance);
            }
        }

        private static bool OnNavigateInputPrefix(InputPressEvent evt) {
            if (!ServerChat.IsOpen || !evt.IsPressed) { return true; }
            if (evt.Type == PageNavType.MenuInGame) { ServerChat.Close(); }
            return false;
        }
    }
}
