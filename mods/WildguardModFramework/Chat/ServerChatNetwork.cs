using System;
using System.Text;
using Fusion;
using RR;
using WildguardModFramework.ModMenu;
using WildguardModFramework.Network;

namespace WildguardModFramework.Chat {
    internal static class ServerChatNetwork {
        private const string ChatChannel = "wmf.chat";
        private const string ConfigChannel = "wmf.config";

        internal static void Init() {
            WmfNetwork.Subscribe(ChatChannel, OnChatReceive);
            WmfNetwork.Subscribe(ConfigChannel, OnConfigReceive);
            WmfNetwork.OnPlayerConfirmed += OnPlayerConfirmed;
        }

        internal static void Dispose() {
            WmfNetwork.Unsubscribe(ChatChannel, OnChatReceive);
            WmfNetwork.Unsubscribe(ConfigChannel, OnConfigReceive);
            WmfNetwork.OnPlayerConfirmed -= OnPlayerConfirmed;
        }

        internal static void SendToHost(string senderName, string text) =>
            WmfNetwork.SendToHost(ChatChannel, Encode(senderName, text));

        // Host calls this to add to its own log and broadcast to all remote confirmed players.
        internal static void HostBroadcast(string senderName, string text) {
            var payload = Encode(senderName, text);
            var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
            foreach (var player in GameModeProtocol.ConfirmedPlayers) {
                if (player == localRef) { continue; }
                WmfNetwork.Send(player, ChatChannel, payload);
            }
        }

        private static void OnChatReceive(PlayerRef sender, byte[] payload) {
            if (!Decode(payload, out var name, out var text)) { return; }
            var runner = PlayerManager.Instance?.Runner;
            if (runner?.IsServer == true) {
                // Host received from a client: add to own log, echo to all remote clients.
                ServerChat.ReceiveMessage(name, text);
                var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
                foreach (var player in GameModeProtocol.ConfirmedPlayers) {
                    if (player == localRef) { continue; }
                    WmfNetwork.Send(player, ChatChannel, payload);
                }
            } else {
                // Client received authoritative echo from host.
                ServerChat.ReceiveMessage(name, text);
            }
        }

        private static void OnConfigReceive(PlayerRef sender, byte[] payload) {
            if (payload.Length == 0) { return; }
            ServerChat.IsEnabled = (payload[0] & 1) != 0;
        }

        private static void OnPlayerConfirmed(PlayerRef player, bool isModded) {
            var runner = PlayerManager.Instance?.Runner;
            if (runner?.IsServer != true || !isModded) { return; }

            var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
            if (player == localRef) {
                // Host sets own flag directly from the host config stepper.
                ServerChat.IsEnabled = HostPageController.Current?.AllowChat ?? true;
                return;
            }

            byte flags = HostPageController.Current?.AllowChat == true ? (byte)1 : (byte)0;
            WmfNetwork.Send(player, ConfigChannel, new[] { flags });
        }

        // Payload: [1 byte: name length][name UTF-8][text UTF-8]
        internal static byte[] Encode(string name, string text) {
            var nameBytes = Encoding.UTF8.GetBytes(name ?? "");
            var textBytes = Encoding.UTF8.GetBytes(text ?? "");
            int nameLen = Math.Min(nameBytes.Length, 255);
            var payload = new byte[1 + nameLen + textBytes.Length];
            payload[0] = (byte)nameLen;
            Buffer.BlockCopy(nameBytes, 0, payload, 1, nameLen);
            Buffer.BlockCopy(textBytes, 0, payload, 1 + nameLen, textBytes.Length);
            return payload;
        }

        internal static bool Decode(byte[] payload, out string name, out string text) {
            name = text = null;
            if (payload == null || payload.Length < 1) { return false; }
            int nameLen = payload[0];
            if (payload.Length < 1 + nameLen) { return false; }
            name = Encoding.UTF8.GetString(payload, 1, nameLen);
            text = Encoding.UTF8.GetString(payload, 1 + nameLen, payload.Length - 1 - nameLen);
            return true;
        }
    }
}
