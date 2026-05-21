# Champion XP system

Source: `game-src/RR.Game.Character/NetworkChampionBase.cs`, `game-src/RR.Game.Character/XPDescriptor.cs`, `game-src/RR.Level/RewardDatabase.cs`

## XPDescriptor struct

```csharp
// RR.Game.Character
public struct XPDescriptor : INetworkStruct {
    public int Amount;        // raw XP points
    public int AbilityPoints; // unspent upgrade points
    public int UltimatePoints;
}
```

## Private backing vs networked state

`NetworkChampionBase` stores XP in a **Fusion-networked state buffer** (offset 18). The private ref property `XP` writes/reads directly from that buffer. The backing field `_XP` is only used for `CopyBackingFieldsToState` / `CopyStateToBackingFields`.

**Do not** access `_XP` via `AccessTools.Field` — mutations will not update the networked state.

## Public API (use these from mods)

| Member | Type | What it does |
|---|---|---|
| `XPAmount` | `int` (read) | Returns current XP amount |
| `XPLevel` | `int` (read) | Returns current level via `RewardDatabase` |
| `AbilityPoints` | `int` (read) | Returns unspent ability upgrade points |
| `SetXP(int newXP)` | `void` | Sets XP to `newXP` and adds the proportional AbilityPoints earned |
| `AddXP(int amount)` | `void` | Adds XP and ability points atomically |
| `SpendAbilityPoint()` | `void` | Decrements AbilityPoints by 1 |
| `XPLevelReset(bool)` | `void` | Zeros all XP and ability data |

## Common patterns

### Set a champion to a specific XP level

```csharp
// After XPLevelReset(), AbilityPoints=0. SetXP sets Amount and
// adds GetXPUpgradePoints(0, targetXP) ability points — equivalent
// to GetXPLevel(targetXP) - 1.
champ.XPLevelReset(XPUnlocksEnabled: false);
champ.SetXP(limits[targetLevel - 1]);
```

### Read XP amount

```csharp
int current = champ.XPAmount;  // NOT champ.XP.Amount (private)
```

### Drain unspent ability points

```csharp
// SpendAbilityPoint writes through the networked property — safe on host.
while (champ.AbilityPoints > 0) { champ.SpendAbilityPoint(); }
```

### XP drip tick (add XP and ability points atomically)

```csharp
// AddXP updates both Amount and AbilityPoints in one networked write.
int gain = newXP - currentXP;
if (gain > 0) { champ.AddXP(gain); }
```

### Level helpers

```csharp
var rdb = RewardDatabase.Instance;
int level = rdb.GetXPLevel(champ.XPAmount);           // level 1..20
int upgradePoints = rdb.GetXPUpgradePoints(old, now); // points earned in delta
var limits = rdb.XPDescriptor.XPLimits;               // List<int> — XP per level boundary
```

## Ghoulag Update changes (breaking)

- `XP` ref property is now `private unsafe ref XPDescriptor` — was previously accessible
- `XP.Amount` reads → use `XPAmount`
- `XP.Amount = value` / `XP.AbilityPoints = value` writes → use `SetXP(value)` or `AddXP(delta)`
- `XP.AbilityPoints = 0` → use `while (champ.AbilityPoints > 0) { champ.SpendAbilityPoint(); }`
