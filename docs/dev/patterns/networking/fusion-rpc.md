# FusionRpcHelper — Targeted RPC Without Broadcast

## What it does

`FusionRpcHelper` (in `WildguardModFramework/Network/`) sends a specific game RPC (`RPC_ErrorMessageAll`, method index 9 on `GameManager`) to **one player only**, without:
- Executing the RPC locally on the host.
- Sending it to any other connected client.

Normal `gm.RPC_ErrorMessageAll(msg)` broadcasts to all clients **and** executes on the host. `FusionRpcHelper.SendErrorMessageTo(runner, gm, target, msg)` skips both of those.

## How it works internally

Fusion RPCs are sent as `SimulationMessage` allocations on the `Simulation` object. The helper:

1. Resolves two private Fusion members once via reflection:
   - `NetworkRunner.Simulation` (property, `internal`) → the `Simulation` instance.
   - `NetworkBehaviour.ObjectIndex` (field, `internal`) → integer index of the `NetworkBehaviour` within its `NetworkObject`, needed for `RpcHeader`.

2. Allocates a `SimulationMessage` directly on the simulation with the correct size.

3. Writes an `RpcHeader` (8 bytes) — `RpcHeader.Create(objectId, objectIndex, methodIndex)` where `methodIndex = 9` matches `RPC_ErrorMessageAll`.

4. Writes the string payload using `ReadWriteUtilsForWeaver.WriteStringUtf8NoHash`.

5. Calls `ptr->SetTarget(target)` — this sets the `Target` field and a `FLAG_TARGET_PLAYER` bit on the message, which makes Fusion route it to only that `PlayerRef`.

6. Calls `runner.SendRpc(ptr)`.

The host never enters the execute path because `SetTarget` skips local execution when a specific target is set.

## What it is NOT

This is not a general "write networked state to one player" mechanism. Fusion's snapshot replication is always broadcast — all clients receive the same state snapshot. `FusionRpcHelper` only works for **one-shot event RPCs** (fire-and-forget method calls), not for making a `[Networked]` property have a different value per client.

If you need per-player state divergence, use the WMF reliable data channel (`WmfNetwork.Send`) or store separate server-side data and send it as a targeted message.

## Reflection handles required

```csharp
// Resolved once in Apply() / TryResolve():
_simulationProp  = AccessTools.Property(typeof(NetworkRunner),    "Simulation");   // internal
_objectIndexField = AccessTools.Field(typeof(NetworkBehaviour),   "ObjectIndex");  // internal
```

Both are `internal` — `AccessTools` bypasses visibility. Warn and fall back to broadcast if either is null.

## Generalising to other RPCs

To send a different RPC this way, change only:
- `methodIndex` in `RpcHeader.Create(...)` — look it up from the Fusion-weaved class. Method indices are assigned in declaration order by the weaver; use ILSpy on `Assembly-CSharp.dll` to find the index of your target RPC (look for `RpcHeader.Create(objectId, behaviourIndex, N)`).
- `objectIndex` — which `NetworkBehaviour` component on the `NetworkObject` owns the RPC. Usually 0 for behaviours directly on the object root.
- Payload format — match what `ReadWriteUtilsForWeaver` writes for the RPC's parameters.

## Usage

```csharp
// Host only — send an error popup to one specific player
FusionRpcHelper.SendErrorMessageTo(runner, GameManager.Instance, targetPlayerRef, "You have been kicked.");
```

Falls back to `RPC_ErrorMessageAll` (broadcast) if reflection fails.
