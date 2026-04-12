# Run Structure

For player-facing names of locations, biomes, and room types see [locations.md](locations.md).

## Overview

A full run spans **two biomes, 19 rooms each = 38 rooms total**. Players choose the starting biome at session start. The second biome is always the other one. After the second BiomeBoss dies, the run ends (or players can loop — see Loops).

**Source:** `game-src/RR.Level/LevelProgressionHandler.cs`

---

## Biomes

| BiomeType | Name |
|---|---|
| 0 | MeatFactory |
| 1 | HarvestOperation |

Biome selection is purely environmental (scenes, music, layout). Both biomes use identical progression algorithms — no mechanical difference. Stored in `LevelProgressionHandler.CurrentBiome` (networked).

At session start, stored in `AppManager.Instance.PlayerSettings.Game_LastSelectedBiome`.

---

## Room types — `LevelType` enum

```csharp
// game-src/RR.Level/LevelType.cs
None, Beginner, Normal, Elite, SuperElite,
Shop, Smuggler, PreRoom, MiniBoss, MidBoss, BiomeBoss,
HealingRoom, Mystery, Tutorial, Lobby, Loop
```

**Combat rooms** (`IsCombatLevel == true`): Beginner, Normal, Elite, SuperElite, MiniBoss, MidBoss, BiomeBoss

**Boss rooms** (`IsBossLevel == true`): MiniBoss, MidBoss, BiomeBoss

**Non-combat rooms**: Shop, Smuggler, PreRoom, HealingRoom, Mystery

---

## Room ordering per biome (19 rooms, indices 0–18)

The sequence is **pre-generated and fixed** at run start. Positions of boss rooms are hardcoded; other positions are randomised within rules.

| Index | Room type | Notes |
|---|---|---|
| 0 | Beginner | Always first |
| 1–3 | Normal | Randomised variants |
| 4 | Smuggler | Before MiniBoss 1 |
| 5 | MiniBoss #1 | Hardcoded (`Descriptor.MiniBoss1Level`) |
| 6–9 | Normal / Elite / Mystery mix | Randomised |
| 10 | MidBoss | Hardcoded |
| 11–14 | Normal / Elite / Mystery mix | Randomised |
| 15 | Smuggler | Before MiniBoss 2 |
| 16 | MiniBoss #2 | Hardcoded (`Descriptor.MiniBoss2Level`) |
| 17 | Transition | Randomised |
| 18 | BiomeBoss | Always last (`LevelCount - 1`) |

Also distributed throughout: **2 Shop rooms**, **2 HealingRooms** (DLC-gated), **~5 Mystery rooms**.

---

## Branching — variant selection, not true branching

After each room, players see **up to 4 doors**. These are **not true branches** — all paths converge back to the fixed boss rooms. Doors let players pick which **variant** of the next predetermined room to play:

- Normal vs Elite vs SuperElite for combat rooms
- Occasionally a Mystery or HealingRoom alternative

`DoorManager.GoToNextLevel(int optionIdx)` handles the vote and transitions. `NextStepOptions` holds the available variants for the next step.

---

## Room rewards — `LevelRewardBase` enum

```csharp
// game-src/RR.Level/LevelRewardBase.cs
LevelSpecial = 0, Perk = 1, Meta = 2, Stat = 3,
BlackCoin = 4, Equipment = 5, Scrap = 6, Glitter = 7, Experience = 8,
Stat_MoveSpeed = 100, Stat_DamageReduction = 101, Stat_Cooldown = 102,
Stat_Critical = 103, Stat_AttackSpeed = 104, Stat_MaxHealth = 105,
Stat_MagicPower = 106, Stat_PhysicalPower = 107
```

Each `LevelDescriptor` has a `RewardBase` and optional `RewardBonus`.

**Boss reward defaults:**
- MiniBoss 1 → `Stat`
- MiniBoss 2 → `Equipment`
- BiomeBoss → `Equipment` + bonus (50/50 Scrap or Glitter)

---

## `LevelProgressionHandler` — key properties

All networked unless noted.

| Property | Type | Description |
|---|---|---|
| `CurrentBiome` | `BiomeType` | Which biome is active |
| `CurrentLevelType` | `LevelType` | Current room type |
| `LevelIndex` | `int` | Current room index in biome (0–18) |
| `LevelCombatIndex` | `int` | Combat room count within current biome |
| `LoopLevelIndex` | `int` | Total rooms across all loop sessions |
| `LoopSessionCount` | `int` | How many loops completed (0 = first run) |
| `LoopLevelCombatIndex` | `int` | Total combat rooms across all loops |
| `IsCombatLevel` | `bool` | Non-networked, derived from `CurrentLevelType` |
| `IsBossLevel` | `bool` | Non-networked, derived from `CurrentLevelType` |
| `Finished` | `bool` | `LevelIndex >= Progression.Count` |
| `NextToFinish` | `bool` | `LevelIndex >= Progression.Count - 1` (at BiomeBoss) |

**Note:** `LevelCombatIndex` does **not** reset when switching from biome 1 to biome 2 (`ResetProgress(resetLoop: false)`). Use `LoopSessionCount == 0` to check if it's the first biome.

---

## Loops — New Game+

After the BiomeBoss, three doors appear:

| Door | Action |
|---|---|
| 0 | Return to lobby — run ends |
| 1 | Loop: same biome (NG+) |
| 2 | Loop: other biome (NG+) |

A loop calls `SessionLoopTrigger(BiomeType nextBiome)`:

```csharp
internal void SessionLoopTrigger(BiomeType nextBiome) {
    CurrentBiome = nextBiome;
    Descriptor = BiomeDescriptors[(int)CurrentBiome];
    ResetProgress(resetLoop: false);  // resets LevelIndex, NOT loop counters
    LoopLevelIndex++;
    LoopSessionCount++;
    if (IsCombatLevel) LoopLevelCombatIndex++;
}
```

`LoopSessionCount` feeds into `DifficultyManager.StageFactor` (exponential difficulty scaling: `1.09^LoopLevelIndex`). The run only truly ends when the player votes for Door 0.
