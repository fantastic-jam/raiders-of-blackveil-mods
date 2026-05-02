using System;
using System.Text;
using Fusion;
using RR;
using WildguardModFramework.Network;

namespace WildguardModFramework.Notifications {
    /// <summary>
    /// Public API for sending corner notifications to all WMF clients in the session.
    ///
    /// Any mod can call Notify() — the message is shown locally and broadcast to every
    /// confirmed WMF player via the "wmf.notification" channel.  WMF versions that do not
    /// subscribe to that channel silently ignore the traffic, so older installs never crash.
    ///
    /// Payload format: [1 byte: NotificationLevel][1 byte: autoClose (0/1)][UTF-8 message]
    /// </summary>
    public static class WmfNotifications {
        /// <summary>Channel name mods can target directly without referencing this class.</summary>
        public const string Channel = "wmf.notification";

        private static ModNotificationOverlay _overlay;
        private static Action<PlayerRef, byte[]> _handler;

        internal static void Init(ModNotificationOverlay overlay) {
            _overlay = overlay;
            _handler = OnChannelMessage;
            WmfNetwork.Subscribe(Channel, _handler);
        }

        /// <summary>
        /// Show a notification to all WMF players in the current session.
        /// Safe to call from any context; no-ops when outside a session.
        ///
        /// The message is shown locally regardless of host/client status.
        /// If the caller is the session host it is also broadcast so all other WMF
        /// clients display it.
        /// </summary>
        public static void Notify(string message, NotificationLevel level = NotificationLevel.Info, bool autoClose = true) {
            _overlay?.Show(message, level, autoClose);
            if (PlayerManager.Instance?.Runner?.IsServer == true) {
                WmfNetwork.Broadcast(Channel, Encode(message, level, autoClose));
            }
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private static void OnChannelMessage(PlayerRef _sender, byte[] payload) {
            if (payload == null || payload.Length < 2) { return; }
            var level = (NotificationLevel)payload[0];
            bool autoClose = payload[1] != 0;
            var message = Encoding.UTF8.GetString(payload, 2, payload.Length - 2);
            _overlay?.Show(message, level, autoClose);
        }

        internal static byte[] Encode(string message, NotificationLevel level, bool autoClose) {
            var msgBytes = Encoding.UTF8.GetBytes(message ?? string.Empty);
            var result = new byte[2 + msgBytes.Length];
            result[0] = (byte)level;
            result[1] = (byte)(autoClose ? 1 : 0);
            if (msgBytes.Length > 0) {
                Buffer.BlockCopy(msgBytes, 0, result, 2, msgBytes.Length);
            }
            return result;
        }
    }
}
