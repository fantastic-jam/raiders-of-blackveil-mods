using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using Fusion.Sockets;
using WildguardModFramework.Registry;
using RR;
using RR.Level;
using RR.UI.Extensions;
using RR.UI.UISystem;
using UnityEngine;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Full game-mode handshake protocol: connection token injection, server-side gating,
    /// client/server join-message and ACK exchange, run-start notifications, and shutdown cleanup.
    /// NetworkPatch wires Harmony hooks and delegates every method body here.
    /// </summary>
    internal static class GameModeProtocol {
        // Token injected by modded clients into StartGameArgs.ConnectionToken.
        private static readonly byte[] WmfToken = Encoding.UTF8.GetBytes("wmf");

        // Reliable data stream types (values 0-8 used by game; 100+ are ours).
        private const DataStreamType StreamTypeJoinMsg = (DataStreamType)101; // server → client: variant ID + join message
        private const DataStreamType StreamTypeAck = (DataStreamType)100;    // client → server: variant ID ACK

        // Server-side player sets. Cleared on shutdown.
        private static readonly HashSet<PlayerRef> _moddedPlayers = new();   // joined with WMF token
        internal static readonly HashSet<PlayerRef> ConfirmedPlayers = new(); // completed handshake (mod enabled + ACK)

        private static NetworkRunner _serverRunner;
        private static bool _joiningWithAutoEnable;
        private static bool _runNotificationShown;

        // ── Client-side ────────────────────────────────────────────────────────────

        /// <summary>Inject token into every StartGame call so the server can identify modded clients.</summary>
        internal static void InjectConnectionToken(ref StartGameArgs args) {
            args.ConnectionToken = WmfToken;
        }

        /// <summary>
        /// Friendly popup when joining a session whose name advertises a required mod the client lacks.
        /// Returns true to proceed with the original JoinPlaySession call, false to cancel.
        /// </summary>
        internal static bool ValidateJoinSession(Guid joinGameSessionId, string serverName, string sessionPassword) {
            if (_joiningWithAutoEnable) { return true; }

            var requiredMode = FindClientRequiredGameMode(serverName);
            if (requiredMode == null) { return true; }

            var localMod = ModScanner.AllDiscovered.FirstOrDefault(m => m.Guid == requiredMode.PluginGuid);
            bool isInstalled = localMod != null && localMod.IsManaged;

            if (!isInstalled) {
                UIManager.Instance?.Popup?.ShowOK(null,
                    "Mod Required",
                    $"This session requires the \"{requiredMode.DisplayName}\" game mode mod.\n\nInstall it to join this session.");
                return false;
            }

            if (!WmfConfig.IsEnabled(localMod.Guid)) {
                UIManager.Instance?.Popup?.ShowOK(null,
                    "Mod Disabled",
                    $"This session requires \"{requiredMode.DisplayName}\" but you have it disabled in WMF.\n\nEnable it in the Mods menu to join.");
                return false;
            }

            localMod.Enable();
            WmfMod.PublicLogger.LogInfo($"WMF: auto-enabled \"{requiredMode.DisplayName}\" for required session.");
            _joiningWithAutoEnable = true;
            try {
                BackendManager.Instance.JoinPlaySession(joinGameSessionId, serverName, sessionPassword);
            }
            finally {
                _joiningWithAutoEnable = false;
            }
            return false;
        }

        // ── Server-side ────────────────────────────────────────────────────────────

        internal static void OnPlayerJoined(PlayerManager playerManager, NetworkRunner runner, PlayerRef playerRef) {
            if (!runner.IsServer) { return; }
            if (playerRef == runner.LocalPlayer) {
                ConfirmedPlayers.Add(playerRef);
                return;
            }

            _serverRunner = runner;

            var selectedId = ModScanner.SelectedGameModeVariantId;
            if (selectedId == null) { return; }

            var activeMode = ModScanner.GameModes.FirstOrDefault(g => g.VariantId == selectedId);
            if (activeMode == null || !activeMode.IsClientRequired) { return; }

            var token = runner.GetPlayerConnectionToken(playerRef);
            if (!HasWmfToken(token)) {
                // Unmodded client: inform them and disconnect after a short delay so they can read the popup.
                WmfMod.PublicLogger.LogInfo($"WMF: {playerRef.PlayerId} has no token — sending error and disconnecting.");
                var gm = GameManager.Instance;
                if (gm != null) {
                    FusionRpcHelper.SendErrorMessageTo(runner, gm, playerRef,
                        $"@This session is running a community mod: \"{activeMode.DisplayName}\".\n\nTo play modded sessions, install WMF from the Raiders of Blackveil Nexus page.");
                }
                playerManager.StartCoroutine(DisconnectAfterDelayCoroutine(runner, playerRef));
                return;
            }

            // Modded client: start handshake.
            _moddedPlayers.Add(playerRef);
            WmfMod.PublicLogger.LogInfo($"WMF: {playerRef.PlayerId} is modded — sending handshake for \"{activeMode.DisplayName}\".");
            var payload = Encoding.UTF8.GetBytes(selectedId + "\n" + (activeMode.JoinMessage ?? ""));
            NetworkManager.Instance?.SendReliableData(playerRef, StreamTypeJoinMsg, payload);
        }

        private static IEnumerator DisconnectAfterDelayCoroutine(NetworkRunner runner, PlayerRef playerRef) {
            yield return new WaitForSeconds(2f);
            WmfMod.PublicLogger.LogInfo($"WMF: disconnecting unmodded {playerRef.PlayerId}.");
            runner.Disconnect(playerRef);
        }

        internal static void OnPlayerLeft(PlayerRef playerRef) {
            _moddedPlayers.Remove(playerRef);
            ConfirmedPlayers.Remove(playerRef);
        }

        // ── Shared reliable data handler ───────────────────────────────────────────

        /// <summary>
        /// Dispatches mod stream types. Must return false for all mod stream types — the game's switch
        /// throws NotImplementedException for unknown values.
        /// </summary>
        internal static bool OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {
            key.GetInts(out var key2, out _, out _, out _);
            var streamType = (DataStreamType)key2;

            if (streamType == StreamTypeJoinMsg) { return HandleJoinMessage(player, data); }
            if (streamType == StreamTypeAck) { return HandleAck(runner, player, data); }
            if (streamType == WmfNetwork.StreamTypeMux) { return WmfNetwork.TryDispatch(player, data); }

            return true; // not our stream — let the game handle it
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────────

        internal static void OnShutdown() {
            _moddedPlayers.Clear();
            ConfirmedPlayers.Clear();
            _serverRunner = null;
            _runNotificationShown = false;
        }

        internal static void OnEventBeginLevel() {
            // Disconnect modded clients who never completed the handshake, before the run starts.
            if (_serverRunner != null && _serverRunner.IsServer) {
                var activeMode = ModScanner.GameModes.FirstOrDefault(g => g.VariantId == ModScanner.SelectedGameModeVariantId);
                if (activeMode?.IsClientRequired == true) {
                    foreach (var player in _moddedPlayers.ToList()) {
                        if (!ConfirmedPlayers.Contains(player)) {
                            WmfMod.PublicLogger.LogInfo($"WMF: {player.PlayerId} failed handshake — disconnecting before run start.");
                            _serverRunner.Disconnect(player);
                        }
                    }
                }
            }

            if (_runNotificationShown) { return; }

            var selectedId = ModScanner.SelectedGameModeVariantId;
            if (selectedId == null) { return; }

            var runMode = ModScanner.GameModes.FirstOrDefault(g => g.VariantId == selectedId);
            if (runMode == null || string.IsNullOrEmpty(runMode.RunStartMessage)) { return; }

            _runNotificationShown = true;
            UIManager.Instance?.GetHUDPage()?.CornerNotifications?.AddLevelEvent(
                "@" + runMode.DisplayName, "@" + runMode.RunStartMessage);
        }

        internal static void OnLobbySceneLoadDone() {
            _runNotificationShown = false;
        }

        // ── Private protocol handlers ──────────────────────────────────────────────

        // Client: enable the game mode and show the join popup, then ACK back.
        private static bool HandleJoinMessage(PlayerRef player, ArraySegment<byte> data) {
            var raw = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            var nl = raw.IndexOf('\n');
            var variantId = nl >= 0 ? raw.Substring(0, nl) : raw;
            var joinMessage = nl >= 0 ? raw.Substring(nl + 1) : "";

            var mode = ModScanner.GameModes.FirstOrDefault(g => g.VariantId == variantId);
            if (mode != null) {
                if (ModScanner.SelectedGameModeVariantId != variantId) {
                    mode.Enable();
                    ModScanner.SelectedGameModeVariantId = variantId;
                    WmfMod.PublicLogger.LogInfo($"WMF: join — enabled \"{mode.DisplayName}\".");
                }

                var ackPayload = Encoding.UTF8.GetBytes(variantId);
                NetworkManager.Instance?.SendReliableDataToHost(
                    PlayerManager.Instance.LocalPlayerRef, StreamTypeAck, ackPayload);
                WmfMod.PublicLogger.LogInfo($"WMF: sent ACK for \"{mode.DisplayName}\".");
            } else {
                WmfMod.PublicLogger.LogWarning($"WMF: join message for unknown variant \"{variantId}\" — not found locally.");
            }

            if (!string.IsNullOrEmpty(joinMessage)) {
                UIManager.Instance?.Popup?.ShowOK(null, mode?.DisplayName ?? "Game Mode", joinMessage);
            }
            return false;
        }

        // Server: client confirmed their game mode is active.
        private static bool HandleAck(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) {
            if (!runner.IsServer) { return false; }
            var variantId = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            ConfirmedPlayers.Add(player);
            WmfMod.PublicLogger.LogInfo($"WMF: ACK from {player.PlayerId} for \"{variantId}\".");
            return false;
        }

        private static bool HasWmfToken(byte[] token) {
            if (token == null || token.Length < WmfToken.Length) { return false; }
            for (int i = 0; i < WmfToken.Length; i++) {
                if (token[i] != WmfToken[i]) { return false; }
            }
            return true;
        }

        private static RegisteredGameMode FindClientRequiredGameMode(string serverName) {
            if (string.IsNullOrEmpty(serverName)) { return null; }
            int start = serverName.LastIndexOf('[');
            int end = serverName.LastIndexOf(']');
            if (start < 0 || end <= start) { return null; }
            var modeName = serverName.Substring(start + 1, end - start - 1).Trim();
            if (string.IsNullOrEmpty(modeName)) { return null; }
            return ModScanner.GameModes.FirstOrDefault(g => g.IsClientRequired && g.DisplayName == modeName);
        }
    }
}
