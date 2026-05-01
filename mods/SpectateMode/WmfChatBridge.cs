using System;
using System.Collections;
using System.Reflection;
using System.Text;
using Fusion;
using HarmonyLib;
using RR;

namespace SpectateMode {
    /// <summary>
    /// Reflection-only bridge to WildguardModFramework's chat infrastructure.
    /// Lets SpectateMode post a system message to the WMF chat overlay locally
    /// and over the network to all WMF-modded clients, without taking a hard
    /// dependency on WMF — if WMF is not loaded, all calls become no-ops.
    /// </summary>
    internal static class WmfChatBridge {
        private const string ChatChannel = "wmf.chat";

        // WmfNetwork.Send(PlayerRef, string, byte[])
        private static MethodInfo _wmfSend;
        // ServerChat.ReceiveMessage(string, string)
        private static MethodInfo _serverChatReceive;
        // GameModeProtocol.ConfirmedPlayers — internal HashSet<PlayerRef>
        private static FieldInfo _confirmedPlayersField;

        private static bool _resolved;

        internal static bool IsAvailable => _wmfSend != null && _serverChatReceive != null && _confirmedPlayersField != null;

        internal static void Resolve() {
            if (_resolved) { return; }
            _resolved = true;

            var wmfNetwork = AccessTools.TypeByName("WildguardModFramework.Network.WmfNetwork");
            if (wmfNetwork != null) {
                _wmfSend = AccessTools.Method(wmfNetwork, "Send", new[] { typeof(PlayerRef), typeof(string), typeof(byte[]) });
            }

            var serverChat = AccessTools.TypeByName("WildguardModFramework.Chat.ServerChat");
            if (serverChat != null) {
                _serverChatReceive = AccessTools.Method(serverChat, "ReceiveMessage", new[] { typeof(string), typeof(string) });
            }

            var protocol = AccessTools.TypeByName("WildguardModFramework.Network.GameModeProtocol");
            if (protocol != null) {
                _confirmedPlayersField = AccessTools.Field(protocol, "ConfirmedPlayers");
            }

            if (!IsAvailable) {
                SpectateModeMod.PublicLogger.LogInfo(
                    "SpectateMode: WMF chat not available — pre-join notifications will only appear in the BepInEx log.");
            }
        }

        /// <summary>
        /// Host-side. Posts <c>[&lt;server&gt;] text</c> in the local chat overlay and
        /// sends it to every confirmed remote modded client via the <c>wmf.chat</c> channel.
        /// No-op if WMF is not loaded.
        /// </summary>
        internal static void HostNotify(string text) {
            if (string.IsNullOrEmpty(text)) { return; }
            if (!IsAvailable) { return; }

            const string sender = "<server>";

            try {
                _serverChatReceive.Invoke(null, new object[] { sender, text });
            }
            catch (Exception ex) {
                SpectateModeMod.PublicLogger.LogWarning($"SpectateMode: WMF ServerChat.ReceiveMessage failed: {ex.Message}");
            }

            var confirmed = _confirmedPlayersField.GetValue(null) as IEnumerable;
            if (confirmed == null) { return; }

            var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
            var payload = EncodeChatPayload(sender, text);

            foreach (var refObj in confirmed) {
                if (refObj is not PlayerRef target) { continue; }
                if (target == localRef) { continue; }
                try {
                    _wmfSend.Invoke(null, new object[] { target, ChatChannel, payload });
                }
                catch (Exception ex) {
                    SpectateModeMod.PublicLogger.LogWarning(
                        $"SpectateMode: WMF Send to {target.PlayerId} failed: {ex.Message}");
                }
            }
        }

        // wmf.chat payload framing: [1 byte: sender length][sender UTF-8][text UTF-8].
        // Mirrors WildguardModFramework.Chat.ServerChatNetwork.Encode so messages we
        // send are decoded correctly by remote ServerChatNetwork subscribers.
        private static byte[] EncodeChatPayload(string sender, string text) {
            var senderBytes = Encoding.UTF8.GetBytes(sender ?? "");
            var textBytes = Encoding.UTF8.GetBytes(text ?? "");
            int senderLen = Math.Min(senderBytes.Length, 255);
            var payload = new byte[1 + senderLen + textBytes.Length];
            payload[0] = (byte)senderLen;
            Buffer.BlockCopy(senderBytes, 0, payload, 1, senderLen);
            Buffer.BlockCopy(textBytes, 0, payload, 1 + senderLen, textBytes.Length);
            return payload;
        }
    }
}
