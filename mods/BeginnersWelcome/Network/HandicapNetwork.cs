using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using Fusion;
using RR;
using WildguardModFramework.Network;

namespace BeginnersWelcome.Network {
    internal static class HandicapNetwork {
        private const string Channel = "bw.handicaps";

        private static readonly DataContractJsonSerializerSettings _serSettings =
            new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
        private static readonly DataContractJsonSerializer _serializer =
            new DataContractJsonSerializer(typeof(Dictionary<string, int>), _serSettings);

        internal static void Enable() {
            Disable();
            WmfNetwork.Subscribe(Channel, OnReceive);
            WmfNetwork.OnPlayerConfirmed += OnPlayerConfirmed;
            WmfNetwork.OnPlayerLeft += OnPlayerLeft;
        }

        internal static void Disable() {
            WmfNetwork.Unsubscribe(Channel, OnReceive);
            WmfNetwork.OnPlayerConfirmed -= OnPlayerConfirmed;
            WmfNetwork.OnPlayerLeft -= OnPlayerLeft;
        }

        internal static void BroadcastChange() {
            if (!IsHost()) { return; }
            WmfNetwork.Broadcast(Channel, Serialize(BuildSnapshot()));
        }

        private static void OnPlayerConfirmed(PlayerRef player, bool isModded) {
            if (!IsHost()) { return; }
            WmfNetwork.Broadcast(Channel, Serialize(BuildSnapshot()));
        }

        private static void OnPlayerLeft(PlayerRef player) {
            if (!IsHost()) { return; }
            WmfNetwork.Broadcast(Channel, Serialize(BuildSnapshot()));
        }

        private static void OnReceive(PlayerRef sender, byte[] payload) {
            var data = Deserialize(payload);
            if (data == null) { return; }
            HandicapState.Values.Clear();
            foreach (var kv in data) {
                HandicapState.Values[kv.Key] = kv.Value;
            }
        }

        private static bool IsHost() {
            var runner = NetworkManager.Instance?.NetworkRunner;
            return runner != null && runner.IsServer;
        }

        // Builds the dict as the panel would show it: current session players only.
        private static Dictionary<string, int> BuildSnapshot() {
            var players = PlayerManager.Instance?.GetPlayers();
            if (players == null) { return new Dictionary<string, int>(); }
            var snapshot = new Dictionary<string, int>();
            foreach (var p in players) {
                var uuid = p.ProfileUUID.ToString();
                snapshot[uuid] = HandicapState.Values.TryGetValue(uuid, out var v) ? v : 0;
            }
            return snapshot;
        }

        private static byte[] Serialize(Dictionary<string, int> data) {
            using var ms = new MemoryStream();
            _serializer.WriteObject(ms, data);
            return ms.ToArray();
        }

        private static Dictionary<string, int> Deserialize(byte[] bytes) {
            try {
                using var ms = new MemoryStream(bytes);
                return _serializer.ReadObject(ms) as Dictionary<string, int>;
            }
            catch (Exception ex) {
                BeginnersWelcomeMod.PublicLogger.LogWarning($"BeginnersWelcome: Failed to deserialize handicaps: {ex.Message}");
                return null;
            }
        }
    }
}
