using System;
using HandyPurse.Patch;
using RR.Game;
using RR.Game.Items;
using RR.Game.Perk;
using UnityEngine;

namespace HandyPurse {
    internal static class InventoryOrchestrator {
        // Returns false (skip original) when the drop was split into multiple vanilla stacks.
        internal static bool OnDropOwnedItem(
                Inventory inventory,
                ItemDescriptor item,
                PlayerFilter playerToPickup,
                bool useRandomRange,
                Vector3? forceDropStartPos) {
            var g = item?.AsGenericItem;
            if (g == null) { return true; }

            var db = ItemDatabase.Instance;
            if (db == null) { return true; }

            var asset = db.GetAsset(g.AssetID);
            if (asset == null) { return true; }

            int vanillaCap = asset.StackMaximum;
            if (g.Amount <= vanillaCap) { return true; }
            if (HandyPursePatch.ResolveCap(g.ItemType) <= 0) { return true; }

            int assetId = g.AssetID;
            int totalAmount = g.Amount;

            if (!inventory.DeleteOwnedItemLocal(item)) { return true; }

            int remaining = totalAmount;
            while (remaining > 0) {
                int amount = Math.Min(remaining, vanillaCap);
                var descriptor = db.CreateItem(assetId, amount);
                if (descriptor.HasValue) {
                    inventory.DropNewItemToGroundHost(descriptor.Value, playerToPickup, useRandomRange, forceDropStartPos);
                    forceDropStartPos = null;
                }
                remaining -= amount;
            }

            return false;
        }
    }
}
