# Save System & Inventory

**Sources:** `game-src/PlayerGameState.cs`, `game-src/RR/BackendManager.cs`, `game-src/RR.Game/Inventory.cs`, `game-src/PlayerProgression/NPCSmuggler.cs`

---

## `PlayerGameState` — the save object

```csharp
// game-src/PlayerGameState.cs
public class PlayerGameState {
    public Guid PlayerId;
    public long TimeStamp;                                          // -1 = invalidated, 0 = no stamp
    public PlayerProgressionData PlayerProgressionData;            // XP, awards, currency holdings, crafts
    public InventoryCommonBackendData InventoryCommonData;         // Stash (shared across champions)
    public List<InventoryChampionBackendData> InventoryChampionData; // Per-champion gear
    public MysticUnlockedItemsBackendData MysticUnlockedItemsBackendData;
}
```

`InventoryChampionBackendData` — per-champion equipped items and loadout.
`InventoryCommonBackendData` — shared stash inventory.

A null `PlayerGameState` passed to a callback means "fresh/empty save" — the game treats it as a new character.

---

## `PlayerProgressionData` fields

```csharp
// game-src/RR.PlayerProgression/PlayerProgressionData.cs
public int ProgressionDataVersion;
public Guid PlayerUUID;
public PlayerAwardsData PlayerAwards;
public Guid[] OwnedCrafts;
public SortedDictionary<Guid, ProductAttributes> Products;       // unlocked products
public SortedDictionary<Guid, int> CurrencyHoldings;            // blackcoins, scrap, etc.
public SortedDictionary<Guid, int> ProgressionStats;            // stats, XP, etc.
```

---

## `BackendManager` — save/load methods

**Source:** `game-src/RR/BackendManager.cs`

### `SavePlayerGameStates` (line 939)

```csharp
public IEnumerator<WaitForSeconds> SavePlayerGameStates(
    List<PlayerGameState> gameStates,
    Action backendRequestCompleted,
    int retryAttempts = 5,
    bool initiatedByClient = false)
```

Coroutine. Sends `IngressMessagePlayerSaveGameStates` via HTTP with up to 5 retries (3s delay). Calls `backendRequestCompleted` when done.

**Patch as prefix (IEnumerator):** return a substitute empty enumerator and call the callback manually.

```csharp
static bool SavePlayerGameStatesPrefix(
    List<PlayerGameState> gameStates,
    Action backendRequestCompleted,
    ref IEnumerator<WaitForSeconds> __result)
{
    // mutate gameStates here before the save, then return true (let original proceed)
    // OR return false + empty enumerator to fully suppress the save
    __result = Enumerable.Empty<WaitForSeconds>().GetEnumerator();
    backendRequestCompleted?.Invoke();
    return false;
}
```

### `LoadPlayerGameState` (line 1013)

```csharp
public IEnumerator<WaitForSeconds> LoadPlayerGameState(
    Guid playerUUID,
    Action<Guid, PlayerGameState> callback)
```

Same coroutine pattern. `callback(playerUUID, null)` = fresh save.

### `PlaySessionBeginLevel` (line 1246)

```csharp
public void PlaySessionBeginLevel(
    IngressMessagePlaySessionBeginLevel requestBeginLevel,
    Action<bool, IngressResponsePlaySessionBeginLevel> callback)
```

Called when a level (room) starts. Sets `CurrentGameActivity = GameActivity.PlaySession`. Has `ErrorBackendUnreachable` fallback (self-stubs without HTTP).

### `PlaySessionEndRun` (line 1324)

```csharp
public void PlaySessionEndRun(
    IngressMessagePlaySessionEndRun requestRunEnd,
    Action<bool, IngressResponsePlaySessionEndRun> callback)
```

Called when a run (set of levels) ends. Sets `CurrentGameActivity = GameActivity.PlaySession`. Has `ErrorBackendUnreachable` fallback.

### `EndPlaySession` (line 1216)

```csharp
public void EndPlaySession(
    IngressMessagePlaySessionEndSession.EndReason endReason,
    int durationInSec,
    Action<bool> callback)
```

Called at the end of an entire play session. Sets `CurrentGameActivity = GameActivity.Initializing`. No `ErrorBackendUnreachable` fallback — must be patched explicitly for offline/RogueRun scenarios.

### Methods with `ErrorBackendUnreachable` self-stubs (no patch needed for offline)

| Method | Fallback behaviour |
|---|---|
| `PlaySessionBeginLevel` | `CurrentGameActivity = PlaySession; callback(true, empty)` |
| `PlaySessionEndLevel` | `callback(true, null)` |
| `PlaySessionBeginRun` | `callback(true, null)` |
| `PlaySessionEndRun` | `CurrentGameActivity = PlaySession; callback(true, empty)` |
| `SendPlaySessionupdateEvent` | no-op |

### Methods requiring explicit patches for offline/no-save scenarios

| Method | Why |
|---|---|
| `EndPlaySession` | No `ErrorBackendUnreachable` guard |
| `SavePlayerGameStates` | No guard — always attempts HTTP |
| `LoadPlayerGameState` | No guard — always attempts HTTP |
| `Logout` | Called on quit — must suppress when offline |

---

## Death vs extraction — distinguishing save triggers

`SavePlayerGameStates` is called from multiple code paths. To distinguish them in a patch:

**Recommended approach:** Set a flag on your state class from **upstream patches** on the death handler and the extraction/exit handler, then read it inside `SavePlayerGameStates`:

```csharp
internal enum RunEndReason { None, Death, Extraction }
// Set RunEndReason.Death when patching the player death handler
// Set RunEndReason.Extraction when patching the BiomeBoss exit / end-run flow
```

Trace from death event → `PlaySessionEndLevel` → `SavePlayerGameStates` and from BiomeBoss exit door → `PlaySessionEndRun` → `SavePlayerGameStates` to find the exact call sites.

---

## Smuggler — `NPCSmuggler`

`game-src/PlayerProgression/NPCSmuggler.cs`

Inherits from `NPCVendor`. Handles mid-run item extraction.

```csharp
protected override void ActivateVendor(bool activate)
```

**Patch point to disable smugglers:** prefix `ActivateVendor`, return `false` when active. Scope to `NPCSmuggler` specifically — do not patch the base `NPCVendor`.

```csharp
static bool ActivateVendorPrefix() => !YourState.IsActive;
```

Smuggler rooms appear at fixed positions in each biome (indices 4 and 15 — see [run_structure.md](run_structure.md)). The NPC still spawns in the room when `ActivateVendor` is suppressed; consider also patching the UI panel (`InventorySmugglerPanel`) to show a message explaining why the smuggler is unavailable.

---

## Modding notes

- Mutating `gameStates` inside a `SavePlayerGameStates` prefix before `return true` is the cleanest way to intercept what gets saved without fully suppressing the save.
- `PlayerGameState` is a plain serialisable class — you can deep-clone it with `JsonConvert.SerializeObject` / `JsonConvert.DeserializeObject` for snapshotting.
- `InventoryChampionData` is a `List<InventoryChampionBackendData>` — `Clear()` wipes all champion inventories; replace individual entries to restore from a snapshot.
- `BackendManager.Instance` is the singleton.
