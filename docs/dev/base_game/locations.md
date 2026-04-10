# Game Locations & Object Names

Canonical player-facing names mapped to code identifiers. Use these terms consistently in comments, docs, and conversation.

---

## The Liberator

The flying ship that serves as the team's base between raids. Players spawn here after each run (via `RPC_Handle_ReturnToLobby`), change champions, access the planning table, buy from the smuggler, and use the training ground.

- **Code class:** `LobbyManager` (`RR.Level`)
- **LevelType:** `Lobby`
- **Scene manager:** extends `LevelManager`

---

## Planning table

The interactive prop in the Liberator that opens the raid setup flow. Walking up to it shows a prompt (`"RaidPage.prompt-plan"`); collecting it opens the raid map screen.

- **Code class:** `PlanningTablePickup` (`RR.Game.Pickups`)
- **Base type:** `PickupItemWithUI`

---

## Raid map screen

The biome/operation selection screen opened from the planning table. Shows available operation sites and lets the team choose difficulty modifiers before launching a raid. No canonical player-facing name confirmed in code.

- **Code class:** `LobbyRaidMapPage` (`RR.UI.Controls.RaidSelect`)
- **Accessed via:** `LobbyHUDPage.RaidPage.Page.RaidMapPage`

---

## Atlas

A meta-progression NPC on the Liberator. Handles rebel-level-gated product unlocks — including unlocking other NPCs such as the merchant. Players spend rebel currency here to unlock content.

- **Code class:** `NPCRebelLeader` (`PlayerProgression`)
- **Vendor variant:** `NPCVendorRebelLeader` — sells products from configured `ProductGroups`
- **Rebel level stat:** `NPCRebelLeader.RebelLevel` (GUID ref to a `PlayerStat`)
- Atlas's presence in the Liberator also controls whether the merchant and mystic spawn: `LobbyManager.UnlockMerchantAppearance`, `LobbyManager.UnlockMysticAppearance`

---

## Training ground

An area inside the Liberator where players can practise abilities without starting a run. Entry/exit is detected by `LobbyManager` via a `BoxCollider`.

- **Code field:** `LobbyManager.TrainingArea` (`BoxCollider`)
- Players are tracked in `_trainingPlayers` (private `List<Player>` in `LobbyManager`)

---

## Operation sites (biomes)

The decompiled source calls these **biomes** (`BiomeType` enum). The player-facing names are the operation site names.

| Player-facing name | `BiomeType` | Value |
|---|---|---|
| Meat Factory | `MeatFactory` | `0` |
| Harvest Operation | `HarvestOperation` | `1` |
| *(Trashlantis)* | — | Hidden in UI; not yet playable |

Biome selection happens on the raid map screen (`LobbyRaidMapPage`). The two UI elements are `"LocMeatFactory"` and `"LocHarvestOp"`. The selected biome is stored in `LevelProgressionHandler.CurrentBiome` and persisted in `AppManager.Instance.PlayerSettings.Game_LastSelectedBiome`.

See [run_structure.md](run_structure.md) for the full per-biome room sequence.

---

## Room types

The engine uses the `LevelType` enum (`game-src/RR.Level/LevelType.cs`) to classify every room.

| Colloquial name | `LevelType` | Notes |
|---|---|---|
| Combat room | `Normal`, `Elite`, `SuperElite`, `Beginner` | Standard enemy rooms; `IsCombatLevel == true` |
| Mystery room | `Mystery` | Non-combat; random variant (casino, containers, BB, random) |
| Healing room | `HealingRoom` | DLC-gated rest room |
| Shop | `Shop` | Buy items from the merchant |
| Smuggler | `Smuggler` | Smuggler NPC room (two per biome, before each mini-boss) |
| Mini-boss room | `MiniBoss` | Two per biome (indices 5 and 16). `IsBossLevel == true` |
| Mid-boss room | `MidBoss` | One per biome (index 10). `IsBossLevel == true` |
| Biome boss room | `BiomeBoss` | Final room of each biome (index 18). `IsBossLevel == true` |
| Pre-room | `PreRoom` | Transition room before a new area |

**Boss level groupings:**
- `IsBossLevel == true`: `MiniBoss`, `MidBoss`, `BiomeBoss`
- `IsCombatLevel == true`: all of the above plus `Normal`, `Elite`, `SuperElite`, `Beginner`
