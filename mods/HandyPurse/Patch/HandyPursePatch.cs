using System;
using HarmonyLib;
using RR.Game.Items;
using UnityEngine;

namespace HandyPurse.Patch {
    public static class HandyPursePatch {
        public static void Apply(Harmony harmony) {
            var amountMaximumGetter = AccessTools.PropertyGetter(typeof(GenericItemDescriptor), nameof(GenericItemDescriptor.AmountMaximum));
            var amountMaximumPostfix = AccessTools.Method(typeof(HandyPursePatch), nameof(AmountMaximumPostfix));
            harmony.Patch(amountMaximumGetter, postfix: new HarmonyMethod(amountMaximumPostfix));

            var createItemMethod = AccessTools.Method(typeof(ItemDatabase), nameof(ItemDatabase.CreateItem));
            var createItemPrefix = AccessTools.Method(typeof(HandyPursePatch), nameof(CreateItemPrefix));
            var createItemPostfix = AccessTools.Method(typeof(HandyPursePatch), nameof(CreateItemPostfix));
            harmony.Patch(createItemMethod, prefix: new HarmonyMethod(createItemPrefix), postfix: new HarmonyMethod(createItemPostfix));

            HandyPurseMod.PublicLogger.LogInfo("Patched item stack limits for HandyPurse.");
        }

        // Keep inventory merge/split logic aware of custom currency limits.
        public static void AmountMaximumPostfix(GenericItemDescriptor __instance, ref int __result) {
            var cap = ResolveCap(__instance?.ItemType ?? ItemType.Unknown);
            if (cap > __result) {
                __result = cap;
            }
        }

        // Replace the asset stack clamp used when creating network item descriptors.
        public static void CreateItemPrefix(int assetID, ref int amount, bool useStackLimit = true) {
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
