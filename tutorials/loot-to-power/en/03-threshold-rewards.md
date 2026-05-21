# 03 — Threshold Rewards

---

Prerequisite: [02-consume-pickups.md](02-consume-pickups.md)

When a pool reaches its threshold, spawn a reward at the player's feet and reduce the pool by the threshold amount. Overflow carries over.

## Threshold values

The variant controls how much is required before a reward fires.

| Pool | Rush | Frenzy |
|---|---|---|
| Stat | 30 | 80 |
| Perk | 50 | 150 |

Expose these as properties in the controller so they are easy to tune.

```csharp
internal static int StatThreshold => IsRush ? 30 : 80;
internal static int PerkThreshold => IsRush ? 50 : 150;
```

## Step 1: Check thresholds after each pool update

Call `CheckThresholds()` at the end of `AddToPool()`.

```csharp
private static void CheckThresholds(PlayerRef player, (int stat, int perk) pool) {
    // IsServer guard is already in AddToPool — safe to call without a second check.
    if (pool.stat >= StatThreshold) { GrantStat(player);  DrainPool(player, stat: StatThreshold, perk: 0); }
    if (pool.perk >= PerkThreshold) { GrantPerk(player);  DrainPool(player, stat: 0, perk: PerkThreshold); }
}

private static void DrainPool(PlayerRef player, int stat, int perk) {
    _pools.TryGetValue(player, out var current);
    _pools[player] = (current.stat - stat, current.perk - perk);
}
```

Subtracting the threshold rather than zeroing keeps overflow — picking up a large equipment haul that overshoots by 15 points does not waste those points.

## Step 2: Grant a stat reward

Stats are spawned as floor pickups via `RewardManager`. Because the pickup filter in tutorial 02 only intercepts `ItemPickup` and `EquipmentPickup`, stat orbs fall through and are collected normally.

`RewardDatabase.GetRandomFromAllStat()` returns a stat reward ID; pass it directly to `RegisterDropStatID`.

```csharp
using RR.Level;
using RR.Utility;

private static void GrantStat(PlayerRef playerRef) {
    var player = PlayerManager.Instance?.GetPlayer(playerRef);
    if (player?.Champion == null) { return; }

    var pos = new DropPos(
        player.Champion.transform.position,
        player.Champion.transform.position + Vector3.up * 0.5f);

    int statId = RewardDatabase.Instance.GetRandomFromAllStat();
    RewardManager.Instance.RegisterDropStatID(statId, pos, ToFilter(playerRef));
}
```

## Step 3: Grant a perk reward

Perks are granted directly to the champion's `PerkHandler` — no floor pickup, no DropPos. `PerkDatabase.GetRandomPerkAmount()` is server-only, matching the host-only guard already in `AddToPool`.

Use `Category.None` to draw from the full perk pool regardless of champion class, and scale rarity with the variant.

```csharp
using RR.Game.Perk;

private static void GrantPerk(PlayerRef playerRef) {
    var player = PlayerManager.Instance?.GetPlayer(playerRef);
    if (player?.Champion == null) { return; }

    var rarity = IsRush ? Rarity.Common : Rarity.Rare;
    var filter = ToFilter(playerRef);
    var perks  = PerkDatabase.Instance.GetRandomPerkAmount(
        1, Category.None, rarity, filter, ignoreThesePerks: null);

    var perkHandler = player.Champion.GetComponent<PerkHandler>();
    if (perkHandler == null) { return; }

    foreach (var perk in perks)
        perkHandler.CollectPerkOnHost(perk);
}
```

## Step 4: Map PlayerRef to PlayerFilter

`PlayerFilter` is an enum (`Player0`, `Player1`, `Player2`, `AnyPlayer`) used by the game to target specific player slots. Map from `PlayerRef` explicitly rather than casting, since slot indexing may not match `PlayerId` directly.

```csharp
private static PlayerFilter ToFilter(PlayerRef playerRef) =>
    playerRef.PlayerId switch {
        0 => PlayerFilter.Player0,
        1 => PlayerFilter.Player1,
        2 => PlayerFilter.Player2,
        _ => PlayerFilter.AnyPlayer,
    };
```

## Step 4: Clean up pools when the mode ends

Call `Reset()` from `Disable()` to clear pools between sessions.

```csharp
internal static void Reset() {
    IsActive = false;
    IsRush   = false;
    _pools.Clear();
}
```

## Result

Pick up enough scrap or equipment to cross the stat threshold — a stat orb spawns at your feet and is collected normally. Accumulate enough black coins or glitter and a perk is granted directly to your champion with no floor pickup involved.

---

## Pool summary

| Loot type | Pool | Points (Rush / Frenzy) |
|---|---|---|
| Scrap | Stat | ×2 / ×1 per unit |
| Equipment | Stat | selling price ÷ 2 / ÷ 4 |
| Black Coin | Perk | ×5 / ×3 per unit |
| Glitter | Perk | ×10 / ×6 per unit |
| BlackBlood | Perk | ×3 / ×2 per unit |

---

## Next

→ [04-block-dropping.md](04-block-dropping.md) — Prevent players from dropping items to the floor
