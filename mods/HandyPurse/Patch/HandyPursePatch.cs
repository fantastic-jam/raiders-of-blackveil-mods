using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game;
using RR.Game.Items;
using RR.Utility;
using UnityEngine;

namespace HandyPurse.Patch {
    public static class HandyPursePatch {
        internal static bool Disabled { get; private set; }
        internal static void SetDisabled() => Disabled = true;
        internal static void SetEnabled() => Disabled = false;

        private static FieldInfo _itemsArrayField;

        public static void Apply(Harmony harmony) {
            var amountMaximumGetter = AccessTools.PropertyGetter(typeof(GenericItemDescriptor), nameof(GenericItemDescriptor.AmountMaximum));
            var amountMaximumPostfix = AccessTools.Method(typeof(HandyPursePatch), nameof(AmountMaximumPostfix));
            harmony.Patch(amountMaximumGetter, postfix: new HarmonyMethod(amountMaximumPostfix));

            var createItemMethod = AccessTools.Method(typeof(ItemDatabase), nameof(ItemDatabase.CreateItem));
            var createItemPrefix = AccessTools.Method(typeof(HandyPursePatch), nameof(CreateItemPrefix));
            var createItemPostfix = AccessTools.Method(typeof(HandyPursePatch), nameof(CreateItemPostfix));
            harmony.Patch(createItemMethod, prefix: new HarmonyMethod(createItemPrefix), postfix: new HarmonyMethod(createItemPostfix));

            _itemsArrayField = AccessTools.Field(typeof(InventorySyncedItems), "_itemsArray");
            if (_itemsArrayField == null) {
                HandyPurseMod.PublicLogger.LogWarning("Could not find InventorySyncedItems._itemsArray — auto-pickup merge will use vanilla stack caps.");
            } else {
                var mergeToInventoryMethod = AccessTools.Method(typeof(InventorySyncedItems), nameof(InventorySyncedItems.MergeToInventory));
                var mergeToInventoryPrefix = AccessTools.Method(typeof(HandyPursePatch), nameof(MergeToInventoryPrefix));
                harmony.Patch(mergeToInventoryMethod, prefix: new HarmonyMethod(mergeToInventoryPrefix));
            }

            HandyPurseMod.PublicLogger.LogInfo("Patched item stack limits for HandyPurse.");
        }

        // Keep inventory merge/split logic aware of custom currency limits.
        public static void AmountMaximumPostfix(GenericItemDescriptor __instance, ref int __result) {
            if (Disabled) {
                // Protect existing stacks: don't advertise a max lower than the current amount,
                // otherwise the game clamps them down (InventorySyncedItems lines ~77 and ~156).
                // MergeToInventory uses asset.StackMaximum directly, so merges still use vanilla defaults.
                if (__instance != null && __instance.Amount > __result) {
                    __result = __instance.Amount;
                }
                return;
            }
            var cap = ResolveCap(__instance?.ItemType ?? ItemType.Unknown);
            if (cap > __result) { __result = cap; }
        }

        // Replace the asset stack clamp used when creating network item descriptors.
        public static void CreateItemPrefix(int assetID, ref int amount, bool useStackLimit = true) {
            if (Disabled) { return; }
            if (!useStackLimit) {
                return;
            }

            var itemDatabase = ItemDatabase.Instance;
            if (itemDatabase == null) {
                return;
            }

            var asset = itemDatabase.GetAsset(assetID);
            if (asset == null) {
                return;
            }

            var cap = ResolveCap(asset.ItemType);
            if (cap > 0) {
                amount = Mathf.Min(cap, amount);
            }
        }

        // Ensure the created descriptor advertises the same custom stack maximum.
        public static void CreateItemPostfix(ref NetworkItemDescriptor? __result) {
            if (Disabled) { return; }
            if (!__result.HasValue) {
                return;
            }

            var descriptor = __result.Value;
            var cap = ResolveCap(descriptor.ItemType);
            if (cap <= 0) {
                return;
            }

            if (descriptor.Stack > cap) {
                descriptor.Stack = cap;
            }
            descriptor.StackMaximum = cap;
            __result = descriptor;
        }

        // Replace merge logic for managed item types so it respects the HandyPurse cap
        // instead of the vanilla asset.StackMaximum field.
        public static bool MergeToInventoryPrefix(object __instance, int assetID, ChampionType champion, int amount, InventoryItemChanges changes, ref int __result) {
            if (Disabled) { return true; }
            var asset = ItemDatabase.Instance?.GetAsset(assetID);
            if (asset == null) {
                __result = 0;
                return false;
            }

            var cap = ResolveCap(asset.ItemType);
            if (cap <= 0) {
                return true; // not managed — let the original run
            }

            if (cap <= 1) {
                __result = 0;
                return false;
            }

            if (_itemsArrayField.GetValue(__instance) is not List<GenericItemDescriptor> itemsArray) {
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
            __result = merged;
            return false; // skip original
        }

        private static int ResolveCap(ItemType itemType) {
            return itemType switch {
                ItemType.Scrap => HandyPurseMod.ScrapCap,
                ItemType.BlackCoin => HandyPurseMod.BlackCoinCap,
                ItemType.BlackBlood => HandyPurseMod.CrystalCap,
                ItemType.Glitter => HandyPurseMod.CrystalCap,
                _ => 0
            };
        }
    }
}
