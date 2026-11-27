using HarmonyLib;
using RR.Input;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine.InputSystem;

namespace WildguardModFramework.Chat {
    internal static class ServerChatHudPatch {
        internal static void Apply(Harmony harmony) {
            // Pages are built in their PageArgs constructor, but IL2CPP constructors can't be patched.
            // Build the overlay on the first OnActivate of each concrete HUD (after the HUD's own elements → on top).
            var activatePrefix = new HarmonyMethod(typeof(ServerChatHudPatch), nameof(HudActivatePrefix));
            var gameHudActivate = AccessTools.Method(typeof(GameHUDPage), "OnActivate");
            if (gameHudActivate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: GameHUDPage.OnActivate not found — in-raid chat overlay inactive.");
            } else {
                harmony.Patch(gameHudActivate, prefix: activatePrefix);
            }
            var lobbyHudActivate = AccessTools.Method(typeof(LobbyHUDPage), "OnActivate");
            if (lobbyHudActivate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: LobbyHUDPage.OnActivate not found — lobby chat overlay inactive.");
            } else {
                harmony.Patch(lobbyHudActivate, prefix: activatePrefix);
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

        private static void HudActivatePrefix(BaseHUDPage __instance) => ServerChat.OnHudInit(__instance);

        private static string _lastPageType;

        // IL2CPP: __instance is wrapped as the declared parameter type (BaseHUDPage), so C# `is`
        // never matches a subclass — always TryCast to test the native runtime type.
        private static void OnUpdatePostfix(BaseHUDPage __instance) {
            var gameHud = __instance.TryCast<GameHUDPage>();
            if (gameHud == null && __instance.TryCast<LobbyHUDPage>() == null) { return; }
            var typeName = __instance.GetIl2CppType().Name;
            if (typeName != _lastPageType) {
                _lastPageType = typeName;
                WmfMod.PublicLogger.LogInfo($"[Chat] OnUpdate page type: {typeName}");
            }
            ServerChat.SetActivePage(__instance);
            if (ServerChat.IsOpen || ServerChat.SuppressOpen) { return; }
            var kb = Keyboard.current;
            if (kb?.enterKey.wasPressedThisFrame != true && kb?.numpadEnterKey.wasPressedThisFrame != true) { return; }
            WmfMod.PublicLogger.LogInfo("[Chat] Enter detected");
            if (gameHud != null && gameHud.IsAnyPageOpen) { return; }
            ServerChat.Open();
        }

        private static void OnDeactivatePostfix(BaseHUDPage __instance) {
            if (__instance.TryCast<GameHUDPage>() != null || __instance.TryCast<LobbyHUDPage>() != null) {
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
