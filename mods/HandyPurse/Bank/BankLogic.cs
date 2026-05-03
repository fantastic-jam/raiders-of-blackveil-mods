using System;
using System.Collections.Generic;
using System.Text;

namespace HandyPurse.Bank {
    internal struct ItemSlot {
        internal int ItemType;
        internal int AssetId;
        internal int Amount;
    }

    internal static class BankLogic {
        // ItemType int values — mirror RR.Game.Items.ItemType enum
        internal const int TypeBlackCoin = 30;
        internal const int TypeBlackBlood = 31;
        internal const int TypeGlitter = 32;
        internal const int TypeScrap = 40;

        internal static bool IsManagedCurrency(int itemType) =>
            itemType == TypeBlackCoin
            || itemType == TypeBlackBlood
            || itemType == TypeGlitter
            || itemType == TypeScrap;

        /// <summary>
        /// Strips excess above the vanilla cap from each slot and returns topup entries.
        /// Mutates slot amounts in place.
        /// getStackMaximum returns null if the asset is not found or not managed.
        /// </summary>
        internal static List<TopupEntry> ComputeExcess(
                List<ItemSlot> slots,
                Func<int, int?> getStackMaximum) {
            var entries = new List<TopupEntry>();
            for (int i = 0; i < slots.Count; i++) {
                var slot = slots[i];
                if (!IsManagedCurrency(slot.ItemType)) { continue; }
                var max = getStackMaximum(slot.AssetId);
                if (!max.HasValue || slot.Amount <= max.Value) { continue; }
                int excess = slot.Amount - max.Value;
                slot.Amount = max.Value;
                slots[i] = slot;
                entries.Add(new TopupEntry {
                    CurrencyKey = CurrencyName(slot.ItemType),
                    AssetId = slot.AssetId,
                    VanillaAmount = max.Value,
                    Excess = excess,
                    SlotIndex = i,
                });
            }
            return entries;
        }

        /// <summary>
        /// Restores stored excess back into the loaded slots using slot indices.
        /// Returns any entries whose SlotIndex was null or out of bounds — callers should
        /// deposit these to the bank so the excess is not silently lost.
        /// </summary>
        internal static List<TopupEntry> ApplyTopup(List<ItemSlot> slots, List<TopupEntry> entries) {
            List<TopupEntry> unresolved = null;
            if (entries == null) { return new List<TopupEntry>(); }
            foreach (var entry in entries) {
                int idx = entry.SlotIndex ?? -1;
                if (idx < 0 || idx >= slots.Count) {
                    (unresolved ??= new List<TopupEntry>()).Add(entry);
                    continue;
                }
                var slot = slots[idx];
                slot.Amount += entry.Excess;
                slots[idx] = slot;
            }
            return unresolved ?? new List<TopupEntry>();
        }

        /// <summary>
        /// Produces a deterministic, order-independent hash of all managed currency slots.
        /// Slots are sorted by assetId before hashing so reordered inventories with identical
        /// amounts produce the same string.
        /// </summary>
        internal static string ComputeHash(List<ItemSlot> slots) {
            var managed = new List<(int assetId, int amount)>();
            foreach (var slot in slots) {
                if (IsManagedCurrency(slot.ItemType)) {
                    managed.Add((slot.AssetId, slot.Amount));
                }
            }
            managed.Sort((a, b) => a.assetId.CompareTo(b.assetId));
            var sb = new StringBuilder();
            for (int i = 0; i < managed.Count; i++) {
                if (i > 0) { sb.Append('|'); }
                sb.Append(managed[i].assetId);
                sb.Append(':');
                sb.Append(managed[i].amount);
            }
            return sb.ToString();
        }

        private static string CurrencyName(int itemType) => itemType switch {
            TypeBlackCoin => "BlackCoin",
            TypeBlackBlood => "BlackBlood",
            TypeGlitter => "Glitter",
            TypeScrap => "Scrap",
            _ => itemType.ToString(),
        };
    }
}
