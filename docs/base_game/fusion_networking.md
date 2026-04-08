# Fusion Networking in Raiders of Blackveil

Raiders of Blackveil uses **Photon Fusion 1.x in Host mode**: one player is both server and client simultaneously. All other players are pure clients. There is no dedicated server.

## Key Concepts

| Term | Meaning |
|------|---------|
| `runner.IsServer` | True only on the host machine |
| `runner.IsClient` | True on all machines (including host) |
| `HasStateAuthority` | True on the host for ALL NetworkObjects. Clients never have state authority. |
| `LocalPlayer` | The local player's `PlayerRef` |

## How Harmony Patches Interact with Fusion

**Harmony patches run on every machine that has the mod installed.** This is the most important thing to understand when writing networked mod logic.

Most game methods that mutate state are guarded at the top:
```csharp
if (!base.Object.HasStateAuthority || !base.Object.isActiveAndEnabled) { return; }
```

Your Harmony prefix fires **before** this guard. So:
- On the **host**: the guard would pass → your code and the original both run.
- On a **client**: your prefix still fires. If you call any game method that internally writes to a `Networked` property, you must add your own guard or the write will interfere with state reconciliation.

### The NetworkArray / Counter trap

`AddDamageData` in `Health` writes to `ReceivedDamageDataArray` and increments `NetworkedReceivedDamageDataCounter` **unconditionally** — there is no authority check for the write itself. `Render()` on every client polls `_lastVisualizedDamageData < NetworkedReceivedDamageDataCounter` to display floating numbers.

If your patch calls `AddDamageData` without a guard:
1. Client's prefix fires → `AddDamageData` → counter advances locally → `Render()` advances `_lastVisualizedDamageData`.
2. Server's authoritative counter arrives (also incremented for the same event) — client's `_lastVisualizedDamageData` is already ahead → entry is skipped → **no display on client**.

**Fix**: always guard NetworkArray writes with `HasStateAuthority`:
```csharp
if (__instance.Object.HasStateAuthority) {
    __instance.AddDamageData(0f, statusEffect, attackerActorId);
}
```

The server writes once → the networked counter and array sync to all clients → `Render()` on every client displays the entry correctly.

## Networking Patterns

### Pattern 1 — Networked Properties & NetworkArray (state sync)

Used for: damage numbers, heal numbers, dodge text, health bars.

- `[Networked]` properties and `NetworkArray<T>` are written by the state authority and automatically synced every simulation tick.
- Clients read the synced values in `Render()` (runs every frame, not just simulation ticks).
- **Do not write from clients.** Writes are either reconciled away or cause the display skipping bug described above.

```csharp
// Safe pattern for any code that touches Networked properties:
if (healthInstance.Object.HasStateAuthority) {
    healthInstance.AddDamageData(value, type, attackerId);
}
```

### Pattern 2 — Fusion RPCs

Used for: one-shot events that all clients need to react to (game start, error messages, lobby close).

RPCs are methods on `NetworkBehaviour` subclasses with `[Rpc(RpcSources.X, RpcTargets.Y)]`.

The game's most useful RPC for mods:

```csharp
// RpcSources.StateAuthority → RpcTargets.All
// Shows an error popup AND disables player controls on every client.
// Use only for fatal/blocking messages — it prevents player input.
GameManager.Instance?.RPC_ErrorMessageAll("Your message here");
```

You cannot define new RPCs from a mod (the Fusion weaver is not run on mod assemblies). You can only call RPCs on existing game `NetworkBehaviour` instances.

### Pattern 3 — Reliable Data (mod channel)

Used for: custom mod-to-mod signaling, join messages, game mode confirmation.

These are point-to-point reliable UDP messages, separate from Fusion's simulation state. No authority restrictions — any connected peer can send to any other.

**Low-level (game's API):**
```csharp
// Server → specific client
NetworkManager.Instance?.SendReliableData(playerRef, (DataStreamType)101, bytes);

// Client → server
NetworkManager.Instance?.SendReliableDataToHost(localRef, (DataStreamType)100, bytes);
```

Stream type values 0–8 are used by the game. Use 100+ for mods. The game's `OnReliableDataReceived` switch throws `NotImplementedException` for unknown values — patch it with a prefix that returns `false` for your stream types.

**High-level (WMF's mux channel, stream type 102):**
```csharp
// Any mod can subscribe to a named channel
WmfNetwork.Subscribe("mygame:event", (from, bytes) => {
    var msg = Encoding.UTF8.GetString(bytes);
    UIManager.Instance?.Popup?.ShowOK(null, "Event", msg);
});

// Server → all confirmed modded clients
WmfNetwork.Broadcast("mygame:event", Encoding.UTF8.GetBytes("Hello!"));

// Client → server
WmfNetwork.SendToHost("mygame:event", Encoding.UTF8.GetBytes("pong"));
```

`ConfirmedPlayers` in `GameModeProtocol` tracks which players have ACKed the game mode — `Broadcast` only sends to these players.

## Communication Patterns by Direction

### Server → specific client

```csharp
// Via WmfNetwork (recommended for mods)
WmfNetwork.Send(playerRef, "mygame:notify", Encoding.UTF8.GetBytes("You got an item!"));

// Via low-level reliable data
NetworkManager.Instance?.SendReliableData(playerRef, (DataStreamType)101, bytes);
```

The client-side handler receives it via `Subscribe` (mux) or `OnReliableDataReceivedPrefix` (low-level).

### Server → all clients (broadcast)

```csharp
// Modded clients only (requires WMF on all)
WmfNetwork.Broadcast("mygame:event", bytes);

// All clients, including unmodded — use sparingly, disables player controls
GameManager.Instance?.RPC_ErrorMessageAll("message");

// Networked state (implicit broadcast — write on server, all clients read in Render())
if (health.Object.HasStateAuthority) { health.AddDamageData(...); }
```

### Client → server

```csharp
WmfNetwork.SendToHost("mygame:request", bytes);
// or low-level:
NetworkManager.Instance?.SendReliableDataToHost(PlayerManager.Instance.LocalPlayerRef, (DataStreamType)100, bytes);
```

### Sending UI Messages — Quick Reference

| Need | Solution | Notes |
|------|----------|-------|
| Fatal error, blocks input | `GameManager.Instance.RPC_ErrorMessageAll(msg)` | Disables player control — avoid for non-fatal messages |
| Popup on one specific client | `WmfNetwork.Send(playerRef, channel, bytes)` + handler showing `Popup.ShowOK` | Targeted, modded clients only |
| Popup on all modded clients | `WmfNetwork.Broadcast(channel, bytes)` + handler | Modded clients only |
| Floating damage/text number | `AddDamageData(value, type, id)` on state authority | State syncs automatically; clients display in `Render()` |
| HUD corner notification | `UIManager.Instance.GetHUDPage()?.CornerNotifications?.AddLevelEvent(title, body)` | Call locally on each client; use `Broadcast` to trigger remotely |

## Checklist for Networked Mod Code

- [ ] Does my Harmony prefix touch `Networked` properties or `NetworkArray`? Add `HasStateAuthority` guard.
- [ ] Am I trying to show something on all clients? Use `Broadcast` (modded) or an existing RPC (all players).
- [ ] Am I calling a game method that itself has an authority guard? My prefix fires before the guard — decide whether to run the logic only on the host.
- [ ] Am I using stream types 100+? Patch `OnReliableDataReceived` to return `false` for your types, or the game will throw `NotImplementedException`.
