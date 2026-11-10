using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using RR;
using UnityEngine;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Detects which remote players run WMF without touching the Fusion connection token.
    /// <para>
    /// The game puts the joiner's profile GUID in the token (exactly 16 bytes) and uses it to
    /// re-admit raid members mid-run, so WMF must leave it alone. WMF can't send an unsolicited
    /// message either: the vanilla <c>NetworkManager.OnReliableDataReceived</c> throws on unknown
    /// stream types, on hosts and clients alike.
    /// </para>
    /// Flow: the WMF host advertises itself with a Fusion session property; a WMF client that sees
    /// it (and only then — an unmodded host would throw) sends a hello on <see cref="StreamTypeHello"/>.
    /// The host resolves each remote joiner as modded on hello, or unmodded after a timeout.
    /// </summary>
    internal static class PresenceProtocol {
        internal const string SessionPropertyKey = "wmf";
        internal const int ProtocolVersion = 1;
        internal const DataStreamType StreamTypeHello = (DataStreamType)103; // client → server

        // Client polls the session properties this long before concluding the host has no WMF.
        private const float PropertyPollSeconds = 5f;
        private const float PropertyPollInterval = 0.25f;
        // Host waits this long for a hello — longer than the client poll so a slow property sync still counts.
        private const float HelloTimeoutSeconds = 8f;

        // Host-side. Hello may arrive before PlayerManager.OnPlayerJoined — keep it until the join.
        private static readonly HashSet<PlayerRef> _earlyHellos = new();
        private static readonly Dictionary<PlayerRef, Action<PlayerRef, bool>> _awaiting = new();

        // ── Host ──────────────────────────────────────────────────────────────

        /// <summary>Advertises this host as WMF to every client that connects. Host mode only.</summary>
        internal static void Advertise(NetworkRunner runner) {
            if (runner == null || !runner.IsServer || runner.GameMode != GameMode.Host) { return; }
            try {
                var ok = runner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty> {
                    { SessionPropertyKey, ProtocolVersion },
                });
                WmfMod.PublicLogger.LogInfo($"WMF: advertised WMF host in session properties (ok={ok}).");
            }
            catch (Exception ex) {
                WmfMod.PublicLogger.LogWarning($"WMF: could not advertise WMF host — remote players will be seen as unmodded. {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves a remote joiner: <paramref name="onResolved"/> is called once with isModded=true when
        /// their hello arrives, or isModded=false after <see cref="HelloTimeoutSeconds"/>.
        /// </summary>
        internal static void AwaitHello(PlayerRef playerRef, Action<PlayerRef, bool> onResolved) {
            if (_earlyHellos.Remove(playerRef)) {
                onResolved(playerRef, true);
                return;
            }
            _awaiting[playerRef] = onResolved;
            WmfMod.Runner.StartCoroutine(HelloTimeoutCoroutine(playerRef));
        }

        internal static bool HandleHello(NetworkRunner runner, PlayerRef playerRef) {
            if (!runner.IsServer) { return false; }
            WmfMod.PublicLogger.LogInfo($"WMF: hello from {playerRef.PlayerId}.");
            if (_awaiting.TryGetValue(playerRef, out var onResolved)) {
                _awaiting.Remove(playerRef);
                onResolved(playerRef, true);
            } else {
                _earlyHellos.Add(playerRef);
            }
            return false; // consumed — the game's switch throws on unknown stream types
        }

        private static IEnumerator HelloTimeoutCoroutine(PlayerRef playerRef) {
            yield return new WaitForSecondsRealtime(HelloTimeoutSeconds);
            if (!_awaiting.TryGetValue(playerRef, out var onResolved)) { yield break; }
            _awaiting.Remove(playerRef);
            WmfMod.PublicLogger.LogInfo($"WMF: no hello from {playerRef.PlayerId} — treating as unmodded.");
            onResolved(playerRef, false);
        }

        internal static void OnPlayerLeft(PlayerRef playerRef) {
            _earlyHellos.Remove(playerRef);
            _awaiting.Remove(playerRef);
        }

        internal static void Reset() {
            _earlyHellos.Clear();
            _awaiting.Clear();
        }

        // ── Client ────────────────────────────────────────────────────────────

        /// <summary>Client: once connected, say hello if (and only if) the host advertises WMF.</summary>
        internal static void OnConnectedToServer(NetworkRunner runner) {
            if (runner == null || runner.IsServer) { return; }
            WmfMod.Runner.StartCoroutine(SendHelloWhenAdvertisedCoroutine(runner));
        }

        private static IEnumerator SendHelloWhenAdvertisedCoroutine(NetworkRunner runner) {
            var deadline = Time.realtimeSinceStartup + PropertyPollSeconds;
            while (runner != null && runner.IsRunning && Time.realtimeSinceStartup < deadline) {
                if (HostAdvertisesWmf(runner)) {
                    NetworkManager.Instance?.SendReliableDataToHost(
                        runner.LocalPlayer, StreamTypeHello, BitConverter.GetBytes(ProtocolVersion));
                    WmfMod.PublicLogger.LogInfo("WMF: host runs WMF — hello sent.");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(PropertyPollInterval);
            }
            WmfMod.PublicLogger.LogInfo("WMF: host does not advertise WMF — staying silent.");
        }

        private static bool HostAdvertisesWmf(NetworkRunner runner) {
            var info = runner.SessionInfo;
            return info != null && info.IsValid && info.Properties != null && info.Properties.ContainsKey(SessionPropertyKey);
        }
    }
}
