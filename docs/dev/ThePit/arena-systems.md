# ThePit — Arena Systems Reference

## Traps

### Classes

`TrapBase` (global namespace, `game-src/TrapBase.cs`) — base for all traps. It's a `NetworkBehaviour`.

- `TrapBase.AllTraps` — static `List<TrapBase>` populated in `Spawned()`, cleared in `Despawned()`.
- `TrapBase.TurnOffTrap()` — public, sets the networked `_state` to `TrapStates.TurnedOff`. State machine never leaves this state once set.
- Must be called server-side (state is Fusion-networked via `base.Ptr`).

Subclasses in the scene: `TrapAoE`, `TrapSawBlades`, `TrapExplosion`, `TrapBasePushable`, etc.

### When to disable

Best hook: `EnemySpawnManager.SceneInit` postfix (server-only). By this point all pre-placed `NetworkBehaviour` objects in the scene are Spawned and present in `AllTraps`.

```csharp
foreach (var trap in TrapBase.AllTraps)
    trap?.TurnOffTrap();
```

### Activation lifecycle

Traps subscribe to `LevelEvent_AllEnemiesKilled` (via `GameEvents.AddListener`) to self-disable after combat. They transition: `Ready → TriggerDelay → Active → Triggered → Cooldown`. `TurnedOff` is terminal — it bypasses the whole machine.

---

## Perk Chests (Shrines)

### Classes

| Class | Namespace | File |
|---|---|---|
| `ShrineHandler` | `RR.Level` | `game-src/RR.Level/ShrineHandler.cs` |
| `ShrineItem` | `RR.Level` | `game-src/RR.Level/ShrineItem.cs` |
| `GamePerkSelectPage` | `RR.UI.Pages` | `game-src/RR.UI.Pages/GamePerkSelectPage.cs` |
| `PerkHandler` | `RR.Game.Perk` | `game-src/RR.Game.Perk/PerkHandler.cs` |

### Normal spawn flow

`RewardManager.Activate()` → `ShrineHandler.Activate(param, levelType)` → `PerPlayerShrineData.Activate(runner)` → `runner.Spawn(ShrineItemPrefab, ...)` at `GameObject.Find("PerkChestSpawnPoint{SlotIndex}")`.

The `PerkChestSpawnPoint{0,1,2}` GameObjects **must exist in the scene**.

### ShrineHandler state machine

`_state: ShrineState` — private field (not Fusion-networked):

```
NotInitialized → Initialized (SceneInit) → Active (Activate()) → Finished (CheckFinished())
```

`Activate()` only proceeds from `Initialized` or `Finished` — safe to call multiple times for multi-round flow.

When `CheckFinished()` returns true inside `FixedUpdateNetwork()`, the handler:
1. Fires `GameEvents.GetGameEvent("LevelEvent_ShrineFinished").Raise()`  
2. Sets `_state = Finished`

### ShrineItem states

`Init → AnimStart → SelectCategory → AnimOpen → AnimPerkDrop → SelectPerk → Finished`

`ShrineItem.Finished` property returns `State == Finished`. When finished, the shrine is inert but still physically in the scene — despawn via `runner.Despawn(shrineItem.Object)`.

`FindObjectsOfType<ShrineItem>()` is the easiest way to find all active shrines for cleanup.

### Double-perk ("IsDoublePickup")

Driven by `ShrineHandler.IsTwoPerkSelection`, set inside `Activate()`:
```csharp
IsTwoPerkSelection = levelType == SuperElite || levelType == MidBoss || ...
```
Pass `LevelType.None` → `IsTwoPerkSelection = false` → each chest gives exactly 1 perk.

### Unmodded client compatibility

**Physical chest spawn is required for unmodded clients.** `ShrineItem` is a `NetworkBehaviour` — spawned by the host via `runner.Spawn()`, synced to all clients. The native `GamePerkSelectPage` UI appears on all clients from the game's own event handling.

The UI-only path (driving `GamePerkSelectPage` directly) only works on clients with the mod installed.

### Multi-round chest flow pattern

```csharp
// Block door until all rounds complete.
ThePitState.ChestPhaseActive = true;

GameEvents.AddListener(gameObject,
    GameEvents.GetGameEvent("LevelEvent_ShrineFinished"), OnShrineFinished);

for (int round = 0; round < rounds; round++) {
    if (round > 0) {
        // Despawn previous round's shrine items.
        foreach (var s in FindObjectsOfType<ShrineItem>())
            if (s?.Object != null && s.Object.IsValid)
                runner.Despawn(s.Object);
        yield return null; // Let despawn propagate.
    }
    _chestRoundDone = false;
    ShrineHandler.Instance.Activate(string.Empty, LevelType.None);
    yield return new WaitUntil(() => _chestRoundDone);
}

ThePitState.ChestPhaseActive = false;
// Open door manually.
DoorManager.Instance?.Activate(string.Empty);
```
