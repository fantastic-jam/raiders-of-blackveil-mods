using UnityEngine.InputSystem;

namespace PlayerEmotes {
    internal static class EmoteInput {
        internal static void OnUpdate() {
            if (Keyboard.current[PlayerEmotesMod.CfgEmoteKey.Value].wasPressedThisFrame) {
                EmoteController.TriggerNetwork();
            }
        }
    }
}
