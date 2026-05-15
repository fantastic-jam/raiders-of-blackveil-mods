using Fusion;
using RR;
using RR.UI.Controls.HUD.Overlay;
using RR.UI.UISystem;
using UnityEngine;
using WildguardModFramework.Network;

namespace PlayerEmotes {
    internal static class EmoteController {
        private const string Channel = "player-emotes.emote";

        internal static void TriggerNetwork() {
            var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
            var payload = new[] { (byte)localRef.PlayerId, (byte)0 };
            WmfNetwork.SendToHost(Channel, payload);
        }

        internal static void OnEmoteReceived(PlayerRef sender, byte[] payload) {
            if (payload.Length < 2) { return; }

            var runner = PlayerManager.Instance?.Runner;
            if (runner == null) { return; }

            if (runner.IsServer) {
                WmfNetwork.Broadcast(Channel, payload);
            }

            ShowForPlayer(payload[0], payload[1] == 0 ? "Hi!" : "?");
        }

        private static void ShowForPlayer(byte senderId, string text) {
            var player = PlayerManager.Instance?.GetPlayers()?.Find(p => (byte)p.PlayerId == senderId);
            var champion = player?.PlayableChampion as MonoBehaviour;
            if (champion == null) { return; }

            var label = EffectText.Allocate(text, 48f, Color.white);
            UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
        }
    }
}
