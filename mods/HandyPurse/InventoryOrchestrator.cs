using System;
using System.Collections.Generic;
using HandyPurse.Patch;
using RR;
using RR.Game;
using RR.Game.Items;
using RR.Game.Perk;
using RR.Utility;
using UnityEngine;

namespace HandyPurse {
    internal static class InventoryOrchestrator {
        // ── AmountMaximum postfix ──────────────────────────────────────────

        internal static void OnAmountMaximum(GenericItemDescriptor instance, ref int result) {
            if (HandyPursePatch.Disabled || !HandyPursePatch.IsHost || instance == null) { return; }
            var localChampion = PlayerManager.Instance?.LocalChampion;
            if (localChampion?.ChampionType == null) { return; }
            bool isLocal = instance.IsInStash
                ? IsLocalPlayerItem(instance)
                : instance.BelongsTo(localChampion.ChampionType);
            if (!isLocal) { return; }
            var cap = HandyPursePatch.ResolveCap(instance.ItemType);
            if (cap > result) { result = cap; }
        }

        // ── CreateItem prefix ──────────────────────────────────────────────

        internal static void OnCreateItemPrefix(int assetID, ref int amount, bool useStackLimit) {
            if (HandyPursePatch.Disabled || !HandyPursePatch.IsHost) { return; }
            if (!useStackLimit) { return; }
            var asset = ItemDatabase.Instance?.GetAsset(assetID);
            if (asset == null) { return; }
            var cap = HandyPursePatch.ResolveCap(asset.ItemType);
            if (cap > 0) {
                amount = Mathf.Min(cap, amount);
            }
        }

        // ── CreateItem postfix ─────────────────────────────────────────────

        internal static void OnCreateItemPostfix(ref NetworkItemDescriptor? result) {
            if (HandyPursePatch.Disabled || !HandyPursePatch.IsHost) { return; }
            if (!result.HasValue) { return; }
            var descriptor = result.Value;
            var cap = HandyPursePatch.ResolveCap(descriptor.ItemType);
            if (cap <= 0) { return; }
            if (descriptor.Stack > cap) {
                descriptor.Stack = cap;
            }
            descriptor.StackMaximum = cap;
            result = descriptor;
        }

        // ── MergeToInventory prefix ────────────────────────────────────────

        // Returns true to run the original, false to skip it.
        internal static bool OnMergeToInventory(
                object instance,
                int assetID,
                ChampionType champion,
                int amount,
                InventoryItemChanges changes,
                ref int result,
                System.Reflection.FieldInfo itemsArrayField) {
            if (HandyPursePatch.Disabled || !HandyPursePatch.IsHost) { return true; }
            var asset = ItemDatabase.Instance?.GetAsset(assetID);
            if (asset == null) {
                result = 0;
                return false;
            }
            var managedCap = HandyPursePatch.ResolveCap(asset.ItemType);
            if (managedCap <= 0) {
                return true; // not managed — let the original run
            }
            // Only the local player gets the elevated HandyPurse cap; other players use vanilla.
            var localChampion = PlayerManager.Instance?.LocalChampion;
            bool isLocal = localChampion?.ChampionType == champion;
            var cap = isLocal ? managedCap : asset.StackMaximum;
            if (cap <= 1) {
                result = 0;
                return false;
            }
            if (itemsArrayField.GetValue(instance) is not List<GenericItemDescriptor> itemsArray) {
                return true; // fallback to original
            }
            int merged = 0;
            foreach (var item in itemsArray) {
                if (item.AssetID != assetID || !item.IsInInventoryOf(champion)) {
                    continue;
                }
                int space = cap - item.Amount;
                if (space > 0) {
                    int toAdd = Math.Min(amount, space);
                    item.Amount += toAdd;
                    changes.AddChanges(item);
                    merged += toAdd;
                    amount -= toAdd;
                    if (amount <= 0) { break; }
                }
            }
            result = merged;
            return false; // skip original
        }

        // ── Drop splitting ─────────────────────────────────────────────────

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

        // ── Helpers ───────────────────────────────────────────────────────

        private static bool IsLocalPlayerItem(GenericItemDescriptor item) {
            var syncedItemsField = HandyPursePatch.SyncedItemsField;
            var itemsArrayField = HandyPursePatch.ItemsArrayField;
            if (syncedItemsField == null || itemsArrayField == null) { return false; }
            var inventory = PlayerManager.Instance?.LocalPlayer?.Inventory;
            if (inventory == null) { return false; }
            var syncedItems = syncedItemsField.GetValue(inventory);
            if (syncedItems == null) { return false; }
            return (itemsArrayField.GetValue(syncedItems) as List<GenericItemDescriptor>)
                ?.Contains(item) ?? false;
        }
    }
}
