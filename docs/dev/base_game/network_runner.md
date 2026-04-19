# NetworkRunner

`NetworkRunner` is Photon Fusion's central simulation object. One instance exists per session. It drives the tick loop, owns the authority model, exposes session metadata, and is the entry point for spawning, despawning, and shutting down.

See [fusion_networking.md](fusion_networking.md) for the authority model, networking patterns, and the `HasStateAuthority` guard rules. This document focuses on the `NetworkRunner` object itself — how to reach it, what it exposes, and what to watch for in mod code.

---

## How to access NetworkRunner

### From a Harmony patch on a NetworkBehaviour subclass

Any `NetworkBehaviour` subclass (e.g. `GameManager`, `PlayerManager`, `Health`) exposes the runner through the inherited instance property:

```csharp
NetworkRunner runner = __instance.Runner;  // via NetworkBehaviour.Runner
```

All ability and arena patches in ThePit use this path:

```csharp
if (__instance.Runner?.IsServer != true) return false;
```

### From static context

`NetworkManager` (singleton, `RR` namespace) holds the runner and exposes it publicly:

```csharp
NetworkRunner runner = NetworkManager.Instance?.NetworkRunner;
```

Use this in patches on non-`NetworkBehaviour` types (UI, managers, etc.) or when you only have access to `static` context.

---

## Key properties

| Property | Type | Notes |
|----------|------|-------|
| `IsServer` | `bool` | `true` only on the host machine. Guard all state-writing code with this. |
| `IsClient` | `bool` | `true` on **all** machines, including the host. Rarely useful as a guard. |
| `IsSharedModeMasterClient` | `bool` | `true` in Shared Mode only. This game uses Host mode — always `false` in practice. |
| `IsSinglePlayer` | `bool` | `true` in single-player mode. Check before applying multiplayer-only logic. |
| `LocalPlayer` | `PlayerRef` | The local player's `PlayerRef`. Available on all machines. |
| `SessionInfo` | `SessionInfo` | Holds `Name` (session name), region, etc. Available after `OnNetworkLaunched`. |
| `Mode` | `GameMode` | `GameMode.Host` in multiplayer, `GameMode.Single` in single-player. |
| `Tick` | `NetworkTick` | Current simulation tick. `.Raw` gives the raw `uint` counter. |
| `Stage` | `SimulationStages` | Current phase of the simulation step. Check `== SimulationStages.Resimulate` to skip side-effectful work during rollback resimulation. |
| `IsShutdown` | `bool` | `true` after `Shutdown()` completes. Used by `BackendManager` to detect orphaned sessions. |

### IsServer vs. HasStateAuthority

These are related but different:

- `runner.IsServer` — a property of the **runner** (the machine). True on exactly one machine.
- `obj.HasStateAuthority` — a property of a **NetworkObject**. In Host mode, the host has authority over all objects, so these are equivalent in practice.

Prefer `__instance.Runner.IsServer` in patches — it is shorter and always correct for Host mode. See [fusion_networking.md](fusion_networking.md) for the full guard rules.

### Stage guard for RPCs

Game RPC methods skip resimulation to avoid duplicate side effects:

```csharp
if (base.Runner.Stage == SimulationStages.Resimulate) return;
```

You do not need this guard in Harmony prefixes because Harmony fires outside the simulation step. It matters only inside `FixedUpdateNetwork()` or `NetworkBehaviour` RPC methods.

---

## Key methods

### Spawn and Despawn

Server-only. Calling these on a client is a no-op.

```csharp
// Spawn a networked prefab
NetworkObject obj = runner.Spawn(prefab, position, rotation, inputAuthority);

// Despawn a networked object
runner.Despawn(networkObject);
```

### Shutdown

```csharp
runner.Shutdown(destroyGameObject: true, ShutdownReason.Ok, forceShutdownProcedure: true);
```

Called by `GameManager` when returning to menu. After shutdown `runner.IsShutdown == true`. The runner GameObject is destroyed by default.

### Disconnect

```csharp
runner.Disconnect(playerRef);  // kick a specific player
```

Used by `GameModeProtocol` to eject unmodded or non-ACK'd clients. Server-only.

### Connection check

```csharp
runner.HasAnyActiveConnections()  // bool
```

Returns `true` if at least one remote peer is connected. Called before sending RPCs to avoid sending into an empty session.

### Reliable data (low-level)

```csharp
// Server → specific client
runner.SendReliableDataToPlayer(playerRef, key, bytes);

// Client → server
runner.SendReliableDataToServer(key, bytes);
```

Prefer the WMF high-level API (`WmfNetwork`) over these. See [fusion_networking.md §Pattern 3](fusion_networking.md#pattern-3--reliable-data-mod-channel).

### Single-player pause

```csharp
runner.SinglePlayerPause();
runner.SinglePlayerContinue();
```

Only meaningful when `runner.IsSinglePlayer == true`.

---

## Lifecycle callbacks (NetworkManager implements INetworkRunnerCallbacks)

`NetworkManager` registers as the `INetworkRunnerCallbacks` listener. Harmony-patch these methods to hook into session lifecycle events. All callbacks receive the `NetworkRunner` as their first parameter.

| Callback | Trigger |
|----------|---------|
| `OnNetworkLaunched` | Fusion fully initialized; `SessionInfo` is now readable |
| `OnPlayerJoined` | A player (including local) joined the session |
| `OnPlayerLeft` | A player disconnected |
| `OnInput` | Input polling tick (runs every simulation step on all machines) |
| `OnShutdown` | Session is shutting down; `ShutdownReason` says why |
| `OnConnectedToServer` | Client successfully connected to host |
| `OnConnectRequest` | Host receives incoming connection request with `byte[]` token |
| `OnConnectFailed` | Connection attempt failed |
| `OnHostMigration` | Host migration event (not used in current game version) |
| `OnReliableDataReceived` | Reliable data arrived (both key-less and `ReliableKey` overloads) |
| `OnSceneLoadStart` / `OnSceneLoadDone` | Scene transitions |
| `OnObjectEnterAOI` / `OnObjectExitAOI` | Object entered/left area-of-interest (server-side) |
| `OnDisconnectedFromServer` | Client lost connection to host |
| `OnSessionListUpdated` | Lobby session list changed |
| `OnUserSimulationMessage` | Custom simulation messages |

### Example — hooking OnPlayerJoined

```csharp
[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnPlayerJoined))]
static class MyJoinPatch
{
    static void Postfix(NetworkRunner runner, PlayerRef playerRef)
    {
        if (!runner.IsServer) return;
        // server-side join logic
    }
}
```

---

## Accessing private members via reflection

Some runner internals are not public. The game itself uses reflection for `Simulation` (needed for manual RPC allocation). Do the same in mod code:

```csharp
// Resolve once in Apply()
private static PropertyInfo _simulationProp;

internal static void Apply()
{
    _simulationProp = AccessTools.Property(typeof(NetworkRunner), "Simulation");
    if (_simulationProp == null) { Log.Warning("NetworkRunner.Simulation not found"); return; }
    // ...
}

// Use in patch
var simulation = (Simulation)_simulationProp.GetValue(runner);
```

Never resolve `AccessTools` handles inline inside a patch method — store them as `private static` fields resolved in `Apply()`.

---

## Quick reference — how to get the runner

| Context | Access |
|---------|--------|
| Patch on a `NetworkBehaviour` (`GameManager`, `PlayerManager`, `Health`, …) | `__instance.Runner` |
| Patch on a non-`NetworkBehaviour` type | `NetworkManager.Instance?.NetworkRunner` |
| Inside `INetworkRunnerCallbacks` callback | First parameter `runner` |
| `WildguardModFramework` static context | `_serverRunner` (cached in `GameModeProtocol.OnPlayerJoined`) |

---

## See also

- [fusion_networking.md](fusion_networking.md) — authority model, `IsServer` guard rules, all three networking patterns, direction-by-direction communication table
- [networking.md (patterns)](../patterns/networking.md) — mod-specific networking pattern guidance and checklist
