using System;
using Fusion;
using Fusion.Sockets;
using HarmonyLib;
using RR;
using RR.Level;
using WildguardModFramework.Chat;
using WildguardModFramework.PlayerManagement;

namespace WildguardModFramework.Network {
    internal static class NetworkPatch {
        internal static void Apply(Harmony harmony) {
            var startGame = AccessTools.Method(typeof(NetworkRunner), nameof(NetworkRunner.StartGame));
            if (startGame == null) {
                WmfMod.PublicLogger.LogWarning("WMF: NetworkRunner.StartGame not found — connection token injection inactive.");
            } else {
                harmony.Patch(startGame, prefix: new HarmonyMethod(typeof(NetworkPatch), nameof(StartGamePrefix)));
            }

            var joinSession = AccessTools.Method(typeof(BackendManager), nameof(BackendManager.JoinPlaySession));
            if (joinSession == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BackendManager.JoinPlaySession not found — client mod validation inactive.");
            } else {
                harmony.Patch(joinSession, prefix: new HarmonyMethod(typeof(NetworkPatch), nameof(JoinPlaySessionPrefix)));
            }

            var onPlayerJoined = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerJoined));
            if (onPlayerJoined == null) {
                WmfMod.PublicLogger.LogWarning("WMF: PlayerManager.OnPlayerJoined not found — join message / confirmation inactive.");
            } else {
                harmony.Patch(onPlayerJoined, postfix: new HarmonyMethod(typeof(NetworkPatch), nameof(OnPlayerJoinedPostfix)));
            }

            var onPlayerLeft = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerLeft));
            if (onPlayerLeft != null) {
                harmony.Patch(onPlayerLeft, postfix: new HarmonyMethod(typeof(NetworkPatch), nameof(OnPlayerLeftPostfix)));
            }

            var reliableDataReceived = AccessTools.Method(typeof(NetworkManager), "OnReliableDataReceived",
                new[] { typeof(NetworkRunner), typeof(PlayerRef), typeof(ReliableKey), typeof(ArraySegment<byte>) });
            if (reliableDataReceived == null) {
                WmfMod.PublicLogger.LogWarning("WMF: NetworkManager.OnReliableDataReceived not found — mod data channel inactive.");
            } else {
                harmony.Patch(reliableDataReceived, prefix: new HarmonyMethod(typeof(NetworkPatch), nameof(OnReliableDataReceivedPrefix)));
            }

            var onShutdown = AccessTools.Method(typeof(NetworkManager), "OnShutdown");
            if (onShutdown != null) {
                harmony.Patch(onShutdown, postfix: new HarmonyMethod(typeof(NetworkPatch), nameof(OnShutdownPostfix)));
            }

            var beginLevel = AccessTools.Method(typeof(BackendManager), "EventBeginLevel");
            if (beginLevel == null) {
                WmfMod.PublicLogger.LogWarning("WMF: BackendManager.EventBeginLevel not found — run-start notification inactive.");
            } else {
                harmony.Patch(beginLevel, postfix: new HarmonyMethod(typeof(NetworkPatch), nameof(EventBeginLevelPostfix)));
            }

            var setUserData = AccessTools.Method(typeof(PlayerManager), "RPC_Handle_SetUserData_All");
            if (setUserData == null) {
                WmfMod.PublicLogger.LogWarning("WMF: PlayerManager.RPC_Handle_SetUserData_All not found — auto-ban enforcement inactive.");
            } else {
                harmony.Patch(setUserData, postfix: new HarmonyMethod(typeof(NetworkPatch), nameof(SetUserDataAllPostfix)));
            }

            var lobbySceneLoadDone = AccessTools.Method(typeof(LobbyManager), "OnSceneLoadDone");
            if (lobbySceneLoadDone == null) {
                WmfMod.PublicLogger.LogWarning("WMF: LobbyManager.OnSceneLoadDone not found — run-start notification reset inactive.");
            } else {
                harmony.Patch(lobbySceneLoadDone, postfix: new HarmonyMethod(typeof(NetworkPatch), nameof(LobbyOnSceneLoadDonePostfix)));
            }
        }

        private static void StartGamePrefix(ref StartGameArgs args) {
            GameModeProtocol.InjectConnectionToken(ref args);
            ServerChat.ClearAll();
        }

        private static bool JoinPlaySessionPrefix(Guid JoinGameSessionId, string serverName, string sessionPassword) =>
            GameModeProtocol.ValidateJoinSession(JoinGameSessionId, serverName, sessionPassword);

        private static void OnPlayerJoinedPostfix(PlayerManager __instance, NetworkRunner runner, PlayerRef playerRef) {
            GameModeProtocol.OnPlayerJoined(__instance, runner, playerRef);
            PlayerManagementController.RefreshOverlay();
        }

        private static void OnPlayerLeftPostfix(PlayerRef playerRef) {
            GameModeProtocol.OnPlayerLeft(playerRef);
            PlayerManagementController.RefreshOverlay();
        }

        private static bool OnReliableDataReceivedPrefix(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) =>
            GameModeProtocol.OnReliableDataReceived(runner, player, key, data);

        private static void OnShutdownPostfix() {
            GameModeProtocol.OnShutdown();
            ServerChat.ClearAll();
        }

        private static void EventBeginLevelPostfix() =>
            GameModeProtocol.OnEventBeginLevel();

        private static void LobbyOnSceneLoadDonePostfix() =>
            GameModeProtocol.OnLobbySceneLoadDone();

        private static void SetUserDataAllPostfix(PlayerManager __instance, PlayerRef playerRef, Guid playerProfileUUID) =>
            PlayerManagementController.OnSetUserData(__instance, playerRef, playerProfileUUID);
    }
}
