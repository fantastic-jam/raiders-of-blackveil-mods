# 02 — Consuming Pickups

---

Prerequisite: [01-game-mode.md](01-game-mode.md)

Intercept every item pickup and convert it to pool points instead of adding it to the player's inventory.

## How pickups work

Every item the player touches — currency, equipment, containers — goes through `PickupItemWithUI.OnCardCollected(StatsManager statsManager)`. This is the single patch point for all pickup types.

Two concrete subclasses handle the cases we care about:

| Class | Pickup type | How to read value |
|---|---|---|
| `ItemPickup` | Scrap, Black Coin, Glitter, BlackBlood | `NetworkItemDescriptor.ItemType` + amount |
| `EquipmentPickup` | Equipment | `EquipmentDescriptor.SellingPrice` (rarity-aware) |

> **Starting items are safe.** Equipment the player owns when entering a run is loaded via `Inventory.InitEquipmentsFromBackend()` — it never passes through `OnCardCollected`. No guard is needed for run-start grants.

## Step 1: Resolve the patch target

Resolve `OnCardCollected` once in `Apply()` and store it as a static field. Never call `AccessTools.Method` inside a patch method.

```csharp
using HarmonyLib;
using RR.UI.Components.Pickup;

internal static class LootToPowerPatch {
    private static MethodBase _onCardCollected;

    internal static void Apply(Harmony harmony) {
        _onCardCollected = AccessTools.Method(typeof(PickupItemWithUI), "OnCardCollected");
        if (_onCardCollected == null) {
            LootToPowerMod.Instance.Logger.LogWarning("LootToPower: OnCardCollected not found — pickup patch skipped.");
            return;
        }

        harmony.Patch(_onCardCollected,
            prefix: new HarmonyMethod(typeof(LootToPowerPatch), nameof(Prefix_OnCardCollected)));
    }

    private static bool Prefix_OnCardCollected(PickupItemWithUI __instance, StatsManager statsManager) =>
        LootToPowerController.OnPickup(__instance, statsManager);
}
```

The prefix returns a `bool`. `true` lets the original `OnCardCollected` run (normal pickup). `false` cancels it (consume).

## Step 2: Route by pickup type

```csharp
using RR.Game.Pickups;

internal static bool OnPickup(PickupItemWithUI pickup, StatsManager statsManager) {
    if (!IsActive) { return true; }

    return pickup switch {
        ItemPickup      item => ConsumeItem(item, statsManager),
        EquipmentPickup eq   => ConsumeEquipment(eq, statsManager),
        _                    => true,    // PerkPickup, stat drops, containers — let through
    };
}
```

Stat orbs are deliberately left through — the threshold system in tutorial 03 spawns them as floor pickups that the player walks over. Perks are granted directly to the champion and never appear as floor pickups, so no exception is needed for them.

## Step 3: Consume currency

```csharp
using RR.Game.Items;

private static bool ConsumeItem(ItemPickup pickup, StatsManager statsManager) {
    var descriptor = pickup.ItemDescriptor;
    int amount     = descriptor.Amount;

    int statPts = 0, perkPts = 0;

    switch (descriptor.ItemType) {
        case ItemType.Scrap:      statPts = amount * (IsRush ? 2 : 1);  break;
        case ItemType.BlackCoin:  perkPts = amount * (IsRush ? 5 : 3);  break;
        case ItemType.Glitter:    perkPts = amount * (IsRush ? 10 : 6); break;
        case ItemType.BlackBlood: perkPts = amount * (IsRush ? 3 : 2);  break;
        default: return true;   // keys, bandages, and anything else — let through
    }

    AddToPool(statsManager, statPts, perkPts);
    return false;
}
```

## Step 4: Consume equipment

The selling price is computed from rarity and equipment class — a legendary item contributes more than a common one with no extra work on our side.

```csharp
using UnityEngine;

private static bool ConsumeEquipment(EquipmentPickup pickup, StatsManager statsManager) {
    int price   = pickup.EquipmentDescriptor.SellingPrice;
    int statPts = Mathf.Max(1, price / (IsRush ? 2 : 4));

    AddToPool(statsManager, statPts, 0);
    return false;
}
```

## Step 5: Accumulate pool totals

Track pools per player on the host. Only the host accumulates — clients do not duplicate the logic.

```csharp
using System.Collections.Generic;
using Fusion;
using RR;

private static readonly Dictionary<PlayerRef, (int stat, int perk)> _pools = new();

private static void AddToPool(StatsManager statsManager, int statPts, int perkPts) {
    var runner = PlayerManager.Instance?.Runner;
    if (runner?.IsServer != true) { return; }

    var playerRef = statsManager.Player?.FusionPlayerRef ?? default;
    if (!playerRef.IsValid) { return; }

    _pools.TryGetValue(playerRef, out var current);
    _pools[playerRef] = (current.stat + statPts, current.perk + perkPts);

    CheckThresholds(playerRef, _pools[playerRef]);
}

## Result

Pick up any currency or equipment during a run with the mode active. Items disappear without entering the inventory. Pool totals accumulate silently on the host.

---

## Next

→ [03-threshold-rewards.md](03-threshold-rewards.md) — Grant stats and perks when a pool threshold is reached
