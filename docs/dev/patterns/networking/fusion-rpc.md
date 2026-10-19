# FusionRpcHelper — Targeted RPC Without Broadcast

Applies to the Fusion version shipped with the 2026-10-19 game update (RPCs built through `RpcBuilder`).
The older `SimulationMessage*` / `RpcHeader` / `runner.SendRpc(ptr)` path no longer exists.

## What it does

`FusionRpcHelper` (in `WildguardModFramework/Network/`) sends `GameManager.RPC_ErrorMessageAll` to **one player only**, without:
- Executing the RPC locally on the host.
- Sending it to any other connected client.

Normal `gm.RPC_ErrorMessageAll(msg)` broadcasts to all clients **and** executes on the host
(`GameManager.ErrorMessage` → popup → `Application.Quit()` on close). Never use the broadcast as a fallback.

## How the weaver sends an RPC now

`game-src/RR/GameManager.cs` — weaved body of `RPC_ErrorMessageAll`:

```csharp
int payloadSize = Fusion.RpcDataWriter.GetPayloadSize(message);
using (Fusion.RpcBuilder b = Runner.CreateRpcBuilder(1480208655u /* [Rpc(Key=...)] */, payloadSize, this, PlayerRef.Invalid)) {
    RpcInvokeInfo info = b.Prepare(out var writer);
    if (info.SendMessageResult == RpcSendMessageResult.Sent) { writer.Write(message); b.Send(); }
}
if (info.LocalInvokeResult == RpcLocalInvokeResult.Invoked) ErrorMessage(message);
```

The last `CreateRpcBuilder` argument is the **target player**. With a valid `PlayerRef`:
- `RpcBuilder.CheckLocalInvoke` returns `TargetPlayerIsNotLocal` on the host, so the host does not run it.
- `RpcBuilder.CheckSend` adds only that player's connection and sets `SimulationMessageHeaderFlags.HasTargetPlayer`.

Results: `RpcSendMessageResult.Sent` (0), `TargetPlayerNotAvailable`, `TargetObjectNotInPlayerInterest`, `PayloadSizeExceeded` (> 512 bytes on non-large channels), and so on.

## Why a DynamicMethod

`NetworkRunner.CreateRpcBuilder(uint, int, NetworkBehaviour, PlayerRef)` is `internal` and returns
`internal ref struct RpcBuilder`; `RpcDataWriter` is also an `internal ref struct`. Ref structs cannot be boxed, so
`MethodInfo.Invoke` cannot call them. WMF emits the same call sequence as a `DynamicMethod` with `skipVisibility: true`
(locals of ref-struct type are fine in IL):

```csharp
private delegate int SendStringRpc(NetworkRunner runner, NetworkBehaviour behaviour, PlayerRef target, uint key, string payload);

var dm = new DynamicMethod("WMF_SendTargetedStringRpc", typeof(int),
    new[] { typeof(NetworkRunner), typeof(NetworkBehaviour), typeof(PlayerRef), typeof(uint), typeof(string) },
    typeof(FusionRpcHelper).Module, skipVisibility: true);
// IL: builder = runner.CreateRpcBuilder(key, RpcDataWriter.GetPayloadSize(payload), behaviour, target);
//     try { info = builder.Prepare(out writer); if (info.SendMessageResult == 0) { writer.Write(payload); builder.Send(); } }
//     finally { builder.Dispose(); }
//     return (int)info.SendMessageResult;
```

See `mods/WildguardModFramework/Network/FusionRpcHelper.cs` for the full emitter.

## RPC key

Read it from the attribute instead of hard-coding it. The key is stable only while the method signature is unchanged:

```csharp
uint key = AccessTools.Method(typeof(GameManager), nameof(GameManager.RPC_ErrorMessageAll))
    .GetCustomAttribute<RpcAttribute>().Key;   // 1480208655 in the 2026-10-19 build
```

## Generalising to other RPCs

Change the key, the target behaviour and the payload writes. `RpcDataWriter` has `Write(...)` overloads and
`GetPayloadSize(...)` helpers per payload type. Copy the exact write sequence from the weaved method body in `game-src/`.
The receiving client runs the weaved invoker (`<Name>_0040Invoker<Key>`), which reads the payload in the same order.

## What it is NOT

This is not a general "write networked state to one player" mechanism. Snapshot replication is always broadcast.
It only works for **one-shot event RPCs**. For per-player data use the WMF reliable data channel (`WmfNetwork.Send`).

## Usage

```csharp
// Host only — send an error popup to one specific (typically unmodded) player
FusionRpcHelper.SendErrorMessageTo(runner, GameManager.Instance, targetPlayerRef, "@You need mod X.");
```

If the Fusion internals cannot be resolved, nothing is sent (logged warning). The caller still disconnects the player.
