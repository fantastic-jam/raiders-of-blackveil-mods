# Lobby — Plan Table Flow

Documents how the in-lobby "Plan Raid" table works: from the player pressing E through to the run starting.

---

## Interactable

**`PlanningTablePickup`** (`RR.Game.Pickups`) is a `PickupItemWithUI`.

- `OnCardShown` → shows the "E Plan Raid" tooltip.
- `OnCardCollected` (server + local player only) → opens the raid planning UI:
  ```csharp
  LobbyHUDPage hudPage = UIManager.Instance.GetHUDPage("LobbyHUDPage");
  hudPage.RaidPage.Open(TransitionAnimation.None);
  hudPage.RaidPage.Page.LoadDifficultyParams();
  ```

---

## Raid Planning UI

**`LobbyRaidPage`** (`RR.UI.Pages`) hosts two sub-pages:

1. **`LobbyRaidMapPage`** — player selects biome (Harvest Operation / Meat Factory).  
   "Prepare →" button calls `OpenDifficultyPage()`.

2. **`LobbyRaidDifficultyPage`** — player selects difficulty + danger modifiers.  
   "Start" button sets `_submitChoice = true` and calls `OnCloseRequest`.

On `OnDeactivate`, if `_submitChoice == true`, `OnTravelClicked.Invoke(this)` fires.

**`LobbyHUDPage.OnInit`** wires `OnTravelClicked`:
```csharp
RaidPage.Page.OnTravelClicked = _ => GameEvents.GetGameEvent("GameEvent_RaidSelected").Raise();
```

`LobbyRaidPage` has `DisablePlayerInput = PlayerInputEnableScope.Disable` — the game disables all player controls while it is open.

---

## GameEvent_RaidSelected

**`LobbyManager.Handle_GameEventRaidSelected`** (server only):
1. Reads `SelectedDifficulty`, `DiffRisky.Value`, and all danger modifier values from the raid pages.
2. Sets `DifficultyManager.Instance.Difficulty`, `DangerRisky`, and all danger modifiers.
3. Sets `LevelProgressionHandler.CurrentBiome` from `RaidMapPage.Page.SelectedBiome`.
4. Sets `IsRaidReadyToGo = true` (networked property — replicated to all clients automatically).
5. Starts a `_startRaidTimer` (0.4 s).

No RPC is fired here — difficulty/biome are written as server-authoritative state.

---

## Ready Gathering

After `IsRaidReadyToGo` is set, `FixedUpdateNetwork` (server) polls on each timer expiry:

```
_startRaidTimer expires
  → AllPlayersReady() ?  (proximity check against _startLocation for all occupied slots)
      yes  → Handle_InputEventStartGame()
      no, local player still near table → restart timer (0.4 s loop)
      no, local player left  → IsRaidReadyToGo = false, reset to NotYet
```

`AllPlayersReady()` sets `PlayerReady[slot]` directly (networked array) — not via RPC. A slot with no player is auto-marked `Ready`.

The table glows (`LobbyStartPlaceEmbarkReady`) while `IsRaidReadyToGo == true` — standard vanilla logic, not patched.

---

## InputEvent_StartGamePressed

Fired by the "Start" debug button or when `AllPlayersReady()` returns true.
**`Handle_InputEventStartGame`** (server only):
1. `PlayerProgressionManager.Instance.OnLobbyEnded()`
2. `PlayerManager.Instance.RPC_SavePlayerGameStateLocally()`
3. `StatsManager.TriggerGlobalEvent(CharacterEvent.OnLevelExit, TriggerParams.Null)`
4. `RPC_TriggerCutsceneRaidStart()`

---

## Cutscene & Load

**`RPC_TriggerCutsceneRaidStart`** (all clients):
- Plays the `Level_Exit_Timeline` cutscene.
- Closes `RaidPage`.
- On cutscene stop → server raises `_startGameEvent` → level load begins.

---

## Skipping the UI (mod use)

To replicate the post-biome-selection behaviour without opening `LobbyRaidPage`, call `Handle_GameEventRaidSelected` directly via reflection (it's `private`):

```csharp
// In Apply():
_handleRaidSelectedMethod = AccessTools.Method(typeof(LobbyManager), "Handle_GameEventRaidSelected");

// Server only — call after setting difficulty:
DifficultyManager.Instance.Difficulty = Difficulty.Normal;
DifficultyManager.Instance.DangerRisky = 0;
var lobbyManager = GameManager.Instance.GetLobbyManager();
if (lobbyManager != null) {
    _handleRaidSelectedMethod?.Invoke(lobbyManager, null);
}
```

`Handle_GameEventRaidSelected` sets `_raidSetup = RaidSetupState.Closed`, `IsRaidReadyToGo = true`, and starts `_startRaidTimer` (0.4 s). The proximity-poll loop then fires the cutscene RPC when all players are ready.

**Ghoulag Update breaking change:** `LobbyManager.RPC_Handle_RaidSetupDone` no longer exists.
Use `Handle_GameEventRaidSelected` (via reflection) instead.

---

## Key classes

| Class | Namespace | Role |
|---|---|---|
| `PlanningTablePickup` | `RR.Game.Pickups` | E-interact entry point |
| `LobbyHUDPage` | `RR.UI.Pages` | HUD container; owns `RaidPage` |
| `LobbyRaidPage` | `RR.UI.Pages` | Map + difficulty sub-pages |
| `LobbyRaidMapPage` | `RR.UI.Controls.RaidSelect` | Biome selection |
| `LobbyRaidDifficultyPage` | `RR.UI.Controls.RaidSelect` | Difficulty + danger |
| `LobbyManager` | `RR.Level` | Event handler; owns ready state |
| `DifficultyManager` | `RR.Level` | Stores difficulty/biome for the run |
