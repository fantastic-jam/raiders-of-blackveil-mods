# Networking

## `IsServer` guard — at collection level, not per-item

```csharp
// Good — one check, then loop
var players = PlayerManager.Instance?.GetPlayers();
if (players == null || players.Count == 0) return;
if (!players[0].Inventory.Object.Runner.IsServer) return;

foreach (var player in players) {
    StripInventory(player.Inventory);
}

// Bad — guard inside the loop
foreach (var player in players) {
    if (!player.Inventory.Object.Runner.IsServer) continue; // wrong
}
```

`players[0].Inventory.Object.Runner` is the Fusion `NetworkRunner` — it is the same runner for all players in the session, so checking it once is correct.

## `__instance.Runner?.IsServer` in ability patches

For patches on `NetworkBehaviour` methods, use the instance's runner directly:

```csharp
private static void DoHitPostfix(RhinoAttackAbility __instance) {
    if (__instance.Runner?.IsServer != true) { return; }
    ...
}
```

The `?` null-propagation handles the case where the runner has not yet been assigned (e.g. the behaviour is patched but the network session has not started).

## Game mode is Host — `IsServer` is the host check

Fusion `GameMode.Host` is used. `IsServer` returns true on the host. `IsSharedModeMasterClient` is always false. There is no dedicated server. See `docs/base_game/fusion_networking.md` for detail.

## `PlayerManager.Instance` null-check

`PlayerManager.Instance` can be null during startup, shutdown, or before level setup completes. Always null-propagate: `PlayerManager.Instance?.GetPlayers()`. If `players` is null, return early — do not proceed to `players[0]`.

## Re-entrant guard — `[ThreadStatic]`, not `static bool`

```csharp
[ThreadStatic] private static bool _rerolling;

static void SomePostfix(...) {
    if (_rerolling) return;
    _rerolling = true;
    try { ... }
    finally { _rerolling = false; }
}
```

Plain `static bool` is not safe if Fusion ever calls from multiple threads.
