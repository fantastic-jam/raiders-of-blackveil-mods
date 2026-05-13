# 05 — Networking

---

Prerequisite: [04-local-labels.md](04-local-labels.md)

Send the emote over the session so every player sees the label above the sender's character.

## How WMF networking works

WMF provides a reliable channel multiplexer over the Fusion session. Mods subscribe to named string channels and exchange raw byte payloads.

| Method | Direction |
|---|---|
| `WmfNetwork.SendToHost(channel, payload)` | Client → host |
| `WmfNetwork.Broadcast(channel, payload)` | Host → all WMF clients |
| `WmfNetwork.Send(target, channel, payload)` | Host → one client |

The pattern for a broadcast emote: client sends to host → host broadcasts to everyone → all clients display the label.

## Step 1: Subscribe and unsubscribe

Store the handler delegate as a field so `Unsubscribe` removes the exact same instance. Wire it up in `Enable()`/`Disable()` in `PlayerEmotesMod.cs`.

Add the field alongside the other static fields:

```csharp
private static Action<PlayerRef, byte[]> _onEmote;
```

Update `Enable()` and `Disable()`:

```csharp
public void Enable() {
    Disabled = false;
    _onEmote = EmoteController.OnEmoteReceived;
    WmfNetwork.Subscribe("player-emotes.emote", _onEmote);
    PlayerEmotesPatch.Patch(_harmony);
    // … #if DEV_HOTRELOAD block unchanged …
}

public void Disable() {
    Disabled = true;
    WmfNetwork.Unsubscribe("player-emotes.emote", _onEmote);
    PlayerEmotesPatch.Unpatch();
    // … #if DEV_HOTRELOAD block unchanged …
}
```

> **Channel naming**: use a namespaced format (`your-mod.channel`) to avoid collisions with other mods.

## Step 2: Send on keypress

Replace `TriggerLocal()` in `EmoteController.cs` with a network-aware version. The pressing client sends the emote ID and its own `PlayerId` to the host:

```csharp
internal static void TriggerNetwork() {
    var localRef = PlayerManager.Instance?.LocalPlayerRef ?? default;
    var payload  = new[] { (byte)localRef.PlayerId, (byte)0 };   // byte 1 = emote id
    WmfNetwork.SendToHost("player-emotes.emote", payload);
}
```

Update `EmoteInput.OnUpdate()` to call `TriggerNetwork()` instead of `TriggerLocal()`:

```csharp
internal static void OnUpdate() {
    if (Keyboard.current[PlayerEmotesMod.CfgEmoteKey.Value].wasPressedThisFrame)
        EmoteController.TriggerNetwork();
}
```

> The method is `OnUpdate()`, not `Update()` — see tutorial 04 for why.

## Step 3: Handle the received message

Add `OnEmoteReceived` to `EmoteController.cs`. The `sender` parameter is Fusion's `PlayerRef` for whoever sent this message (the emoting client when on host; the host when on client). The payload carries the emoting player's `PlayerId` so all machines know whose head to label.

```csharp
internal static void OnEmoteReceived(PlayerRef sender, byte[] payload) {
    if (payload.Length < 2) { return; }

    var runner = PlayerManager.Instance?.Runner;
    if (runner == null) { return; }

    // Host re-broadcasts so every client receives it
    if (runner.IsServer)
        WmfNetwork.Broadcast("player-emotes.emote", payload);

    // All machines (including host) display the label
    ShowForPlayer(payload[0], payload[1] == 0 ? "Hi!" : "?");
}
```

> **No emoji** — the game font does not render emoji. Use plain text strings. See tutorial 04.

## Step 4: Resolve the sender's position

Look the player up by `PlayerId` (encoded as `payload[0]`) and spawn the label using `EffectText` + `UIManager`. `PlayableChampion` is a `NetworkBehaviour` — cast it to `MonoBehaviour` to access `gameObject`.

```csharp
private static void ShowForPlayer(byte senderId, string text) {
    var player   = PlayerManager.Instance?.GetPlayers()?.Find(p => (byte)p.PlayerId == senderId);
    var champion = player?.PlayableChampion as MonoBehaviour;
    if (champion == null) { return; }

    var label = EffectText.Allocate(text, 48f, Color.white);
    UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
}
```

> Use `GetPlayers().Find(p => ...)` to look up by `PlayerId` rather than reconstructing a `PlayerRef`. `PlayerRef.FromEncoded` exists in Fusion but `GetPlayers()` is simpler and avoids reliance on internal encoding details.

The full `EmoteController.cs`:

```csharp
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
            var payload  = new[] { (byte)localRef.PlayerId, (byte)0 };
            WmfNetwork.SendToHost(Channel, payload);
        }

        internal static void OnEmoteReceived(PlayerRef sender, byte[] payload) {
            if (payload.Length < 2) { return; }

            var runner = PlayerManager.Instance?.Runner;
            if (runner == null) { return; }

            if (runner.IsServer)
                WmfNetwork.Broadcast(Channel, payload);

            ShowForPlayer(payload[0], payload[1] == 0 ? "Hi!" : "?");
        }

        private static void ShowForPlayer(byte senderId, string text) {
            var player   = PlayerManager.Instance?.GetPlayers()?.Find(p => (byte)p.PlayerId == senderId);
            var champion = player?.PlayableChampion as MonoBehaviour;
            if (champion == null) { return; }

            var label = EffectText.Allocate(text, 48f, Color.white);
            UIManager.Instance.RegisterOverlayElement(label, champion.gameObject, Vector3.up * 2f, Vector2.zero);
        }
    }
}
```

## Result

Any player presses the emote key. All connected WMF clients see "Hi!" float and fade above that player's head.

---

## Extending the mod

| Feature | What to change |
|---|---|
| More emotes | Expand the `emoteId` byte; add a picker in the settings panel |
| Cooldown | Track `Time.time` of last send; reject `TriggerNetwork()` if too soon |
| Chat echo | Call `ServerChat.SendMessage()` alongside `TriggerNetwork()` to also post the emote in chat |
