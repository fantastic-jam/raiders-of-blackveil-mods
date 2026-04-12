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
1. Reads `SelectedDifficulty` and `DiffRisky.Value` from the raid pages.
2. Sets `DifficultyManager.Instance.Difficulty` and `DangerRisky`.
3. Sets `LevelProgressionHandler.CurrentBiome` from `RaidMapPage.Page.SelectedBiome`.
4. Calls `RPC_Handle_RaidSetupDone(difficulty, dangerLevel)` — broadcasts to all clients.

---

## RPC_Handle_RaidSetupDone

Runs on all clients:
- Sets `_raidSelected = true` (if more than one player).
- Server-side: calls `RPC_Handle_PlayerReadyEvent(localPlayer.FusionPlayerRef)` — marks the host as ready.

---

## Ready Gathering

**`RPC_Handle_PlayerReadyEvent`** (all → all, authority writes):
- Sets `PlayerReady[slot] = ReadyState.Ready`.
- Calls `UpdateReady()`.
- When `_allPlayersReady` (everyone near the table):
  - Sets `IsReadyForRaid = true`.
  - Starts a `TickTimer` (0.4 s solo, 2 s multiplayer).

The table glows and players must be in range — this is handled by the vanilla ready-area logic, not patched.

---

## InputEvent_StartGamePressed

Fired by the "Start" debug button or when all players confirm.  
**`Handle_InputEventStartGame`** (server only):
1. `PlayerProgressionManager.Instance.OnLobbyEnded()`
2. `PlayerManager.Instance.RPC_SavePlayerGameStateLocally()`
3. `PlayerManager.Instance.SendAllPlayersGameStatesToBackend(OnBackendRequestCompleted)`

**`OnBackendRequestCompleted`** → `RPC_TriggerCutsceneRaidStart()`.

---

## Cutscene & Load

**`RPC_TriggerCutsceneRaidStart`** (all clients):
- Plays the `Level_Exit_Timeline` cutscene.
- Closes `RaidPage`.
- On cutscene stop → server raises `_startGameEvent` → level load begins.

---

## Skipping the UI (mod use)

To replicate the post-biome-selection behaviour without opening `LobbyRaidPage`, call directly on the server:

```csharp
var lph = GameManager.Instance.LevelProgressionHandler;
lph.CurrentBiome = BiomeType.MeatFactory; // already forced by ThePit

DifficultyManager.Instance.Difficulty = Difficulty.Normal;
DifficultyManager.Instance.DangerRisky = 0;

GameManager.Instance.GetLobbyManager()
    .RPC_Handle_RaidSetupDone(Difficulty.Normal, 0);
```

This skips biome/difficulty selection entirely and goes straight to the ready-gathering + glow phase.

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
