# Key Classes & Entry Points

Quick reference for modders — where to find things and how to access them.

---

## Singletons

| Class | Access | Source file |
|---|---|---|
| `AppManager` | `AppManager.Instance` | `game-src/RR/AppManager.cs` |
| `BackendManager` | `BackendManager.Instance` | `game-src/RR/BackendManager.cs` |
| `DifficultyManager` | `DifficultyManager.Instance` | `game-src/RR.Level/DifficultyManager.cs` |
| `GameManager` | `GameManager.Instance` | `game-src/RR/GameManager.cs` |
| `PlayerManager` | `PlayerManager.Instance` | — |
| `NetworkManager` | `NetworkManager.Instance` | — |
| `UIManager` | `UIManager.Instance` | — |
| `AreaManager` | `AreaManager.Instance` | — |
| `EquipmentDatabase` | `EquipmentDatabase.Instance` | — |
| `ItemDatabase` | `ItemDatabase.Instance` | — |
| `PerkDatabase` | `PerkDatabase.Instance` | `game-src/RR.Game.Perk/PerkDatabase.cs` |
| `RewardManager` | `RewardManager.Instance` | — |

`LevelProgressionHandler` is accessed via `GameManager.Instance.LevelProgressionHandler` (not a standalone singleton).

---

## Run & level progression

| What | Where |
|---|---|
| Current biome | `GameManager.Instance.LevelProgressionHandler.CurrentBiome` |
| Current room type | `GameManager.Instance.LevelProgressionHandler.CurrentLevelType` |
| Room index in biome | `GameManager.Instance.LevelProgressionHandler.LevelIndex` |
| Combat room count (current biome) | `GameManager.Instance.LevelProgressionHandler.LevelCombatIndex` |
| Loop count | `GameManager.Instance.LevelProgressionHandler.LoopSessionCount` |
| Is this a boss room? | `GameManager.Instance.LevelProgressionHandler.IsBossLevel` |
| Run is at final boss | `GameManager.Instance.LevelProgressionHandler.NextToFinish` |
| Run is complete | `GameManager.Instance.LevelProgressionHandler.Finished` |
| Rooms until end of biome | `Descriptor.LevelCount - LevelIndex` |
| Full progression sequence | `LevelProgressionHandler.Progression` — `List<List<LevelDescriptor>>` |
| Next room options (branching) | `LevelProgressionHandler.NextStepOptions` |

---

## Difficulty & alarm

| What | Where |
|---|---|
| Alarm level (0–∞) | `DifficultyManager.Instance.AlarmLevel` |
| Base difficulty | `DifficultyManager.Instance.Difficulty` |
| Danger level (static modifier total) | `DifficultyManager.Instance.GetDangerLevel()` |
| Combat time (seconds) | `DifficultyManager.Instance.CombatTimeInSec` |
| Risky modifier level (0–3) | `DifficultyManager.Instance.DangerRisky` |
| Armored modifier level (0–3) | `DifficultyManager.Instance.DangerArmored` |
| NoHelp modifier | `DifficultyManager.Instance.DangerNoHelp` |
| Player count | `PlayerManager.Instance.PlayerCount` |

See [difficulty.md](difficulty.md) for the full modifier list.

---

## Networking

| What | Where |
|---|---|
| Is this the host/server? | `NetworkManager.Instance.Runner.IsServer` |
| Current game mode | `NetworkManager.Instance.Runner.GameMode` (expect `GameMode.Host`) |
| Runner reference (from NetworkBehaviour) | `this.Runner` / `__instance.Runner` |
| Spawn a networked object | `runner.Spawn(prefab, position, rotation, inputAuthority, onBeforeSpawned)` |

All state-changing operations (drops, spawns, saves) must be guarded with `runner.IsServer`.

---

## Player & inventory

| What | Where |
|---|---|
| All active players | `PlayerManager.Instance.GetAllActivePlayers()` |
| Local player ref | `PlayerManager.Instance.LocalPlayerRef` |
| Player GUID | `player.PlayerId` (or similar — verify in `PlayerManager`) |
| Player inventory | `player.GetComponent<Inventory>()` or the `Inventory` NetworkBehaviour on the player object |
| Player health | `player.GetComponent<Health>()` |
| Save object | `PlayerGameState` — assembled by `BackendManager` from `Inventory` + `PlayerProgressionData` |

---

## Loot & drops

| What | Where |
|---|---|
| Enemy drop config (ScriptableObject) | `EnemyDropConfig` — one per biome/enemy type |
| Runtime drop state per player | `EnemyDropRuntime` — constructed with `EnemyDropConfig` + `Player` |
| Test if a drop occurs | `EnemyDropRuntime.TestDropChance(in EnemyInfo, in DifficultyInfo)` |
| Select items to drop | `EnemyDropRuntime.DropItems(runner, in DifficultyInfo, EnemyInfo, DropPos)` |
| Select equipment rarity | `EquipmentDropRuntime.GetNextRarity(...)` |
| Spawn drops (server only) | `RewardDropHandler` — see [loot_and_drops.md](loot_and_drops.md) |

---

## Smugglers

| What | Where |
|---|---|
| NPC class | `NPCSmuggler : NPCVendor` — `game-src/PlayerProgression/NPCSmuggler.cs` |
| Activation hook | `NPCSmuggler.ActivateVendor(bool activate)` |
| UI panel | `InventorySmugglerPanel` — `game-src/RR.UI.Controls.Inventory/InventorySmugglerPanel.cs` |
| Lobby pickup | `SmugglerLobbyPickup` — `game-src/RR.Game.Pickups/SmugglerLobbyPickup.cs` |
| In-game pickup | `SmugglerGamePickup` — `game-src/RR.Game.Pickups/SmugglerGamePickup.cs` |

---

## UI

| What | Where |
|---|---|
| Current HUD page | `UIManager.Instance.GetCurrentHUDPage()` |
| Change page | `UIManager.Instance.ChangePage("PageName")` |
| Popup (OK button) | `UIManager.Instance.Popup.ShowOK(title, message)` |
| Localized string | `LocStringExt.Get("key")` — namespace `RR.UI.Components` |
| Player settings (language etc.) | `AppManager.Instance.PlayerSettings` |
| Alarm level display | `DifficultyFeedbackPanel` on `GameHUDPage` |

---

## Common Harmony patterns in this codebase

```csharp
// Resolve private field once in Apply()
var field = AccessTools.Field(typeof(TargetClass), "_fieldName");

// Prefix to block a method
static bool MethodPrefix() => !YourState.IsActive;  // return false = skip original

// Prefix on IEnumerator to substitute a coroutine
static bool CoroutinePrefix(Action callback, ref IEnumerator<WaitForSeconds> __result) {
    callback?.Invoke();
    __result = Enumerable.Empty<WaitForSeconds>().GetEnumerator();
    return false;
}

// Postfix to observe/modify return value
static void MethodPostfix(ref bool __result) {
    if (!YourState.IsActive || __result) return;
    __result = true;
}

// Server-only guard inside a patch
if (!__instance.Object.Runner.IsServer) return;
```

---

## File locations for common lookups

| Topic | Path |
|---|---|
| Run progression logic | `game-src/RR.Level/LevelProgressionHandler.cs` |
| Difficulty & alarm | `game-src/RR.Level/DifficultyManager.cs` |
| Room types & descriptors | `game-src/RR.Level/LevelType.cs`, `LevelDescriptor.cs` |
| Enemy drop config | `game-src/RR.Game.ItemDrop/EnemyDropConfig.cs` |
| Enemy drop runtime | `game-src/RR.Game.ItemDrop/EnemyDropRuntime.cs` |
| Equipment rarity roll | `game-src/RR.Game.ItemDrop/EquipmentDropRuntime.cs` |
| Drop spawning | `game-src/RR.Level/RewardDropHandler.cs` |
| Save/load | `game-src/RR/BackendManager.cs` |
| Player save object | `game-src/PlayerGameState.cs` |
| Player progression data | `game-src/RR.PlayerProgression/PlayerProgressionData.cs` |
| Inventory | `game-src/RR.Game/Inventory.cs` |
| Health & death | `game-src/RR.Game.Stats/Health.cs` |
| Smuggler NPC | `game-src/PlayerProgression/NPCSmuggler.cs` |
| Perk system | `game-src/RR.Game.Perk/PerkDatabase.cs`, `PerkHandler.cs` |
| Game startup flow | `game-src/RR/AppManager.cs`, `BackendManager.cs` |
| Main menu pages | `game-src/RR.UI.Pages/MenuStartPage.cs` |
| HUD | `game-src/RR.UI.Pages/GameHUDPage.cs` |
