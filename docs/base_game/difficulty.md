# Difficulty System

**Source:** `game-src/RR.Level/DifficultyManager.cs`

---

## Difficulty levels

```csharp
// Difficulty enum
Easy, Normal, Hard, Nightmare
```

Base difficulty is set from `AppManager.Instance.PlayerSettings.Game_Difficulty` at run start.

`DifficultyFactor` (used in AlarmLevel formula):
- Easy → 1
- Normal → 2
- Hard → 3
- Nightmare → 5

---

## Alarm level

A dynamic integer that represents escalating danger during a run. Drives enemy scaling and drop rate modifiers. **Starts at 0 on run start, increases continuously with combat time.**

```csharp
public int AlarmLevel => Mathf.FloorToInt(CoeffWithoutPlayer);

public float CoeffWithoutPlayer =>
    CombatTimeInMin * TimeMultiplier * TimeFactor * StageFactor;

// TimeFactor: scales with difficulty and player count
public float TimeFactor =>
    0.0506f * (float)DifficultyFactor * Mathf.Pow(PlayerManager.Instance.PlayerCount, 0.2f);

// StageFactor: exponential growth per loop (1.0 on first run, higher in NG+)
public float StageFactor =>
    Mathf.Pow(1.09f, GameManager.Instance.LevelProgressionHandler.LoopLevelIndex);

public float CombatTimeInMin => CombatTimePrecise / 60f;
```

`TimeMultiplier` is an inspector-set base multiplier on `DifficultyManager`.

**What it affects:**
- `EnemyLevel = 1 + Floor(CoeffWithoutPlayer / 0.4f)` — enemy stat scaling
- `EnemyDropConfig.DangerLevelModifier` — drop rate multipliers per difficulty tier
- Displayed in `DifficultyFeedbackPanel`

**Reset:** `RunStart()` sets `CombatTimePrecise = 0` and `CombatTimeInSec = 0`. Danger modifier settings are **not** reset.

---

## Danger level — session modifiers

A separate integer computed from difficulty + modifier stack. Set once at session start, does not change during the run.

```csharp
public int GetDangerLevel() =>
    (int)Difficulty * 100                                          // Normal=100, Hard=200, Nightmare=300
    + (DangerRisky > 0         ? RiskyDLAddon[DangerRisky - 1]             : 0)
    + (DangerArmored > 0       ? ArmoredDLAddon[DangerArmored - 1]         : 0)
    + (DangerInjured           ? InjuredDLAddon[0]                          : 0)
    + (DangerCannonFodder      ? CannonFodderDLAddon[0]                     : 0)
    + (DangerEliteChampions > 0? EliteChampionsDLAddon[DangerEliteChampions - 1] : 0)
    + (DangerTrapMaster        ? TrapMasterDLAddon[0]                       : 0)
    + (DangerNoHelp            ? NoHelpDLAddon[0]                           : 0)
    + (DangerExpensive > 0     ? ExpensiveDLAddon[DangerExpensive - 1]      : 0);
```

---

## Difficulty modifiers — networked properties

Selected on `LobbyRaidDifficultyPage` before the run starts. All are networked on `DifficultyManager`.

| Property | Type | Values | Description |
|---|---|---|---|
| `Difficulty` | `Difficulty` | Easy/Normal/Hard/Nightmare | Base difficulty |
| `DangerRisky` | `int` | 0–3 | Risk modifier level |
| `DangerArmored` | `int` | 0–3 | Armored enemies level |
| `DangerInjured` | `NetworkBool` | on/off | Players start injured |
| `DangerCannonFodder` | `NetworkBool` | on/off | Cannon fodder modifier |
| `DangerEliteChampions` | `int` | 0–3 | Elite champion frequency |
| `DangerTrapMaster` | `NetworkBool` | on/off | Trap master modifier |
| `DangerNoHelp` | `NetworkBool` | on/off | No help modifier |
| `DangerExpensive` | `int` | 0–3 | Shop price modifier |

**These are independent of biome selection.** Both biome and difficulty selection happen in the lobby, but on separate pages.

---

## Timing properties (networked)

| Property | Type | Description |
|---|---|---|
| `CombatTimeInSec` | `int` | Seconds in active combat (drives AlarmLevel) |
| `CombatTimePrecise` | `float` | Precise combat seconds |
| `RunTimeInSec` | `float` | Total run time including non-combat |
| `LevelTimeInSec` | `float` | Local only — time in current room |

---

## `DifficultyInfo` struct

Passed around as `in DifficultyInfo diffInfo` in drop/combat code. Contains a snapshot of current difficulty state. Fields include `Difficulty`, `BiomeProgressionRatio`, `LoopCount`, `RelativeDangerLevel`.

Used in `EnemyDropConfig.DangerLevelMultiplier(in DifficultyInfo)` to scale drop chances.

---

## Modding notes

- `DifficultyManager.Instance` is the singleton.
- `AlarmLevel` is public and requires no reflection.
- Danger modifiers are networked — reads are safe on any client, writes must happen on the host/server.
- `RunStart()` is the clean hook for anything that needs to reset state at run start.
