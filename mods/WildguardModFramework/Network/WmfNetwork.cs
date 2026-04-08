using System;
using System.Collections.Generic;
using System.Text;
using Fusion;
using RR;

namespace WildguardModFramework.Network {
    /// <summary>
    /// Channel-102 pub/sub multiplexer. Mods subscribe to named string channels;
    /// WMF routes all traffic over a single owned DataStreamType value.
    ///
    /// Payload framing: [1 byte: channel name length][N bytes: channel name UTF-8][remaining: data]
    /// Channel names are limited to 255 UTF-8 bytes.
    /// </summary>
    public static class WmfNetwork {
        internal const DataStreamType StreamTypeMux = (DataStreamType)102;

        private static readonly Dictionary<string, List<Action<PlayerRef, byte[]>>> _handlers = new();

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Subscribe to a named channel. Handler is called on all received messages for that channel.
        /// Call from your mod's Enable() and pair with Unsubscribe() in Disable().
        /// </summary>
        public static void Subscribe(string channel, Action<PlayerRef, byte[]> handler) {
            if (string.IsNullOrEmpty(channel)) { throw new ArgumentException("Channel name must not be empty.", nameof(channel)); }
            if (handler == null) { throw new ArgumentNullException(nameof(handler)); }

            if (!_handlers.TryGetValue(channel, out var list)) {
                list = new List<Action<PlayerRef, byte[]>>();
                _handlers[channel] = list;
            }
            list.Add(handler);
        }

        /// <summary>
        /// Unsubscribe a previously registered handler. Call from your mod's Disable().
        /// </summary>
        public static void Unsubscribe(string channel, Action<PlayerRef, byte[]> handler) {
            if (string.IsNullOrEmpty(channel) || handler == null) { return; }
            if (_handlers.TryGetValue(channel, out var list)) {
                list.Remove(handler);
            }
        }

        /// <summary>Host → specific client.</summary>
        public static void Send(PlayerRef target, string channel, byte[] payload) {
            var framed = Frame(channel, payload);
            NetworkManager.Instance?.SendReliableData(target, StreamTypeMux, framed);
        }

        /// <summary>Client → host.</summary>
        public static void SendToHost(string channel, byte[] payload) {
            var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
            var framed = Frame(channel, payload);
            NetworkManager.Instance?.SendReliableDataToHost(localRef, StreamTypeMux, framed);
        }

        /// <summary>Host → all clients that have WMF (modded players).</summary>
        public static void Broadcast(string channel, byte[] payload) {
            var framed = Frame(channel, payload);
            foreach (var player in GameModeProtocol.ConfirmedPlayers) {
                NetworkManager.Instance?.SendReliableData(player, StreamTypeMux, framed);
            }
        }

        // ── Internal dispatch ─────────────────────────────────────────────────

        /// <summary>
        /// Called from NetworkPatch.OnReliableDataReceivedPrefix for StreamTypeMux messages.
        /// Returns false (consumed) always — unknown channels are silently dropped.
        /// </summary>
        internal static bool TryDispatch(PlayerRef player, ArraySegment<byte> data) {
            if (data.Count < 2) { return false; }

            var arr = data.Array;
            var offset = data.Offset;

            int nameLen = arr[offset];
            if (data.Count < 1 + nameLen) { return false; }

            var channel = Encoding.UTF8.GetString(arr, offset + 1, nameLen);
            var payloadOffset = offset + 1 + nameLen;
            var payloadCount = data.Count - 1 - nameLen;
            var payload = new byte[payloadCount];
            if (payloadCount > 0) {
                Buffer.BlockCopy(arr, payloadOffset, payload, 0, payloadCount);
            }

            if (_handlers.TryGetValue(channel, out var handlers)) {
                foreach (var handler in handlers) {
                    try { handler(player, payload); }
                    catch (Exception ex) {
                        WmfMod.PublicLogger.LogError($"WmfNetwork: handler for channel \"{channel}\" threw: {ex}");
                    }
                }
            }

            return false; // always consumed — don't fall through to game switch
        }

        // ── Framing ───────────────────────────────────────────────────────────

        private static byte[] Frame(string channel, byte[] payload) {
            if (string.IsNullOrEmpty(channel)) { throw new ArgumentException("Channel name must not be empty.", nameof(channel)); }

            var nameBytes = Encoding.UTF8.GetBytes(channel);
            if (nameBytes.Length > 255) { throw new ArgumentException("Channel name exceeds 255 UTF-8 bytes.", nameof(channel)); }

            var result = new byte[1 + nameBytes.Length + (payload?.Length ?? 0)];
            result[0] = (byte)nameBytes.Length;
            Buffer.BlockCopy(nameBytes, 0, result, 1, nameBytes.Length);
            if (payload != null && payload.Length > 0) {
                Buffer.BlockCopy(payload, 0, result, 1 + nameBytes.Length, payload.Length);
            }
            return result;
        }
    }
}
