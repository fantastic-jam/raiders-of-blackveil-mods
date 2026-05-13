# 04 — Blocking Item Drops

---

Prerequisite: [02-consume-pickups.md](02-consume-pickups.md)

In Loot to Power, all floor pickups are consumed. A player dropping an item from their inventory would turn it into a floor pickup — which the mode would then consume. Block dropping entirely while the mode is active.

## How item dropping works

`Inventory.DropOwnedItemToGroundLocal(ItemDescriptor item, PlayerFilter playerToPickup, bool useRandomRange, Vector3? forceDropStartPos)` is the entry point for a player intentionally dropping an item. It removes the item from the local inventory, then sends an RPC to the host which spawns it as a floor pickup via `RewardManager.RegisterDropItem()`.

A prefix that returns `false` cancels the entire call before anything is removed from the inventory.

## Step 1: Resolve the patch target

```csharp
using HarmonyLib;
using RR.Game;

internal static class DropBlockPatch {
    private static MethodBase _dropOwned;

    internal static void Apply(Harmony harmony) {
        _dropOwned = AccessTools.Method(typeof(Inventory), "DropOwnedItemToGroundLocal");
        if (_dropOwned == null) {
            LootToPowerMod.Instance.Logger.LogWarning("LootToPower: DropOwnedItemToGroundLocal not found — drop block skipped.");
            return;
        }

        harmony.Patch(_dropOwned,
            prefix: new HarmonyMethod(typeof(DropBlockPatch), nameof(Prefix_DropOwned)));
    }

    private static bool Prefix_DropOwned() => LootToPowerController.AllowDrop();
}
```

## Step 2: Allow drops only when the mode is inactive

```csharp
internal static bool AllowDrop() => !IsActive;
```

`!IsActive` is `false` while the mode is running — the prefix returns `false` and cancels the drop. When the mode is off, `!IsActive` is `true` and the original method runs normally.

## Step 3: Register the patch

Call `DropBlockPatch.Apply()` from `EnableVariant()` alongside the existing pickup patch.

```csharp
public void EnableVariant(string variantId) {
    LootToPowerController.Activate(variantId);
    LootToPowerPatch.Apply(_harmony);
    DropBlockPatch.Apply(_harmony);
}
```

Both patches are removed by `_harmony.UnpatchSelf()` in `Disable()` — no extra cleanup needed.

## Result

While Loot to Power is active, players cannot drop items to the floor. Attempting to drag an item out of the inventory has no effect. The item stays in the inventory, safe from the consumption pools.

---

## Patch summary

| Patch class | Target class | Target method | Prefix returns |
|---|---|---|---|
| `LootToPowerPatch` | `PickupItemWithUI` | `OnCardCollected` | `false` to consume, `true` to let through |
| `DropBlockPatch` | `Inventory` | `DropOwnedItemToGroundLocal` | `false` to block, `true` to allow |
