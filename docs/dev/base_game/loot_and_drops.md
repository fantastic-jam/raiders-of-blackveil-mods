# Loot & Drops

**Sources:** `game-src/RR.Game.ItemDrop/EnemyDropConfig.cs`, `EnemyDropRuntime.cs`, `EquipmentDropRuntime.cs`, `RoomRewardDropConfig.cs`, `RewardDropHandler.cs`

---

## Enemy drop system

### Flow

1. Enemy dies → `EnemyDropRuntime.TestDropChance()` decides if a drop occurs
2. If yes → `EnemyDropRuntime.DropItems()` selects which items from the drop table
3. `EquipmentDropRuntime.GetNextRarity()` selects rarity for equipment drops
4. `RewardDropHandler` queues and physically spawns the pickups

### `EnemyDropRuntime.TestDropChance()`

`game-src/RR.Game.ItemDrop/EnemyDropRuntime.cs:91`

Returns `true` if a drop should occur for this enemy kill. Uses `ChanceWithBadLuckProtection` per rank/variant — bad luck protection guarantees a drop every N misses.

**Base drop chances (from `EnemyDropConfig`):**

| Rank | Normal | Elite | SuperElite |
|---|---|---|---|
| Fodder | 5% | 10% | 15% |
| Lieutenant | 10% | 15% | 20% |
| Captain | 20% | 25% | 33% |
| MiniBoss | 100% | — | — |
| Boss | 100% | — | — |

**Multipliers applied to base chance:**
- `ChanceMultiplier` — AnimationCurve over `BiomeProgressionRatio` (0→1 through the biome)
- `DangerLevelMultiplier(diffInfo)` — per difficulty: Easy/Normal/Hard/Nightmare modifiers
- `LoopChanceMultiplier` — AnimationCurve over `diffInfo.LoopCount`

### Drop tables

Each rank has its own `List<ItemDropWithBLP>` inside `EnemyDropConfig`:
- `FodderDrops`, `LieutenantDrops`, `CaptainDrops`, `MiniBossDrops`, `BossDrops`
- Elite enemies also get `EliteExtraDrops`, SuperElites get `SuperEliteExtraDrops`

### Enemy ranks

```csharp
// EnemyRanks enum
Minion, Fodder, Lieutenant, Captain, MiniBoss, Boss
```

`Minion` never drops items (filtered before `TestDropChance` is called).

---

## Equipment rarity selection

`game-src/RR.Game.ItemDrop/EquipmentDropRuntime.cs`

### `GetNextRarity()` signature

```csharp
public Rarity GetNextRarity(
    System.Random random,
    DifficultyInfo difficulty,
    LuckStats luckStats,
    RarityModifier rarityModifier,
    float uniqueChanceAddon,
    out bool beUnique,
    RarityRange? rarityRange = null)
```

### How rarity is selected

1. `RarityChanceRuntime._rarityChance.GetNextRarity()` — weighted roll from base rarity distribution
2. Unique check per rarity tier (using `ChanceWithBadLuckProtection`):
   - Rare unique: ~10% base, 1% BLP floor
   - Epic unique: ~25% base, 1% BLP floor
   - Legendary: 100% unique
   - Mythic: 100% unique
3. Luck modifiers applied: `rarityModifier.RoundingUpChance += luckStats.LootLuck`
4. Optional `RarityRange` clamps the result to min/max rarity bounds

### `RarityModifier` sources

The rarity roll can be biased by:
- `DifficultyInfo` — difficulty-based rarity modifiers from `EnemyDropConfig.GetRarityModifier()`
- Enemy variant modifiers (`EliteModifiers.EquipmentRarityModifier`, `SuperEliteModifiers.EquipmentRarityModifier`)
- Item-type-specific modifiers for Souvenir, PerkRecipe, Equipment

### Special roll flags

- `UberStatChance` — 1.5% base, 0.05% BLP floor
- `LuckyChance` — 5% base, 0.5% BLP floor
- Class suffix chances per roll: Warrior, Assassin, Priest, Guardian, Monk, Warlock, Druid, Mage

---

## Room reward drops

`game-src/RR.Game.ItemDrop/RoomRewardDropConfig.cs` — ScriptableObject

Configures currency and equipment drops at room completion (not enemy kills):

- **Currency ranges**: BlackCoin, Scrap, Glitter — per difficulty
- **Elite/SuperElite bonuses**: Percentage addons to currency
- **Equipment drops**: Rarity bonuses, count, additional drop chances

---

## `RewardDropHandler` — spawning drops

`game-src/RR.Level/RewardDropHandler.cs`

Queue-based system. Must run on server (`runner.IsServer`).

```csharp
var handler = new RewardDropHandler();
handler.Init();

// Add items to the queue
handler.AddEquipment(equipment, dropPos, PlayerFilter.ForPlayer(player));
handler.AddItem(item, dropPos, playerFilter);
handler.AddPerk(perkDescriptor, dropPos, playerFilter);
handler.AddStat(statRewardID, dropPos, playerFilter);

// Spawn one item per call — call in a loop until IsEmpty
while (!handler.IsEmpty)
    handler.DropNextItem(runner);
```

`DropPos` fields:
- `SpawnPos` — final position on NavMesh
- `DropStartPos` — arc origin (visual)
- `RangeMax`, `RangeMin` — correction radius for NavMesh placement

---

## Bad luck protection — `ChanceWithBadLuckProtection`

Used throughout the drop system. Tracks consecutive misses and guarantees a drop by a configurable count.

```csharp
// Clone before modifying — EnemyDropRuntime clones from EnemyDropConfig at construction
var chance = config._fodderNormal.Clone();
chance.SetTempChanceMultiplier(multiplier);
bool dropped = chance.NextRandom(random);
```

---

## Biome progression drop curve

`EnemyDropConfig.ChanceMultiplier` is an `AnimationCurve` over `BiomeProgressionRatio`:
```
BiomeProgressionRatio = (float)(LevelCombatIndex - 1) / 20f  // 0.0 at room 0 → 1.0 at room 21
```

Curve is evaluated at `DifficultyInfo.BiomeProgressionRatio` to scale the base drop chance as you go deeper into the biome.

---

## Modding notes

- **Patch `TestDropChance` postfix** to add extra rolls or multiply drop frequency — `ref bool __result`.
- **Patch `GetNextRarity` postfix** to bias rarity — `ref Rarity __result`. Check `RogueRunState.IsActive` first.
- **Do not modify `EnemyDropConfig` fields directly** unless you cloned the instance — changes affect all enemies globally.
- `RewardDropHandler` is a plain class (not MonoBehaviour) — instantiate freely on the server, use `runner.Spawn()` only for perk/flask pickups via the `OnBefore*Spawned` callbacks.
