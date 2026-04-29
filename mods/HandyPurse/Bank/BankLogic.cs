using System;
using System.Collections.Generic;
using System.Text;

namespace HandyPurse.Bank {
    internal enum TopupApplyStatus { Applied, HashMismatch, LayoutChanged }

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
        /// Strips excess above the vanilla cap from each slot, returns topup entries and the
        /// post-clamp hash. Mutates slot amounts in place.
        /// getStackMaximum returns null if the asset is not found or not managed.
        /// </summary>
        internal static (List<TopupEntry> entries, string hash) ComputeExcess(
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
            return (entries, entries.Count > 0 ? ComputeHash(slots) : string.Empty);
        }

        /// <summary>
        /// Restores topup amounts into the correct slots.
        /// On hash mismatch or bad layout: deposits everything to bank.
        /// On successful restore: verifies each slot reached the expected total;
        /// any shortfall is also deposited to bank (safeguard against partial restores).
        /// </summary>
        internal static (TopupApplyStatus status, List<BankEntry> bankDeposit) ApplyTopup(
                List<ItemSlot> slots,
                TopupCompartment compartment) {
            var bankDeposit = new List<BankEntry>();
            if (compartment == null || compartment.Entries.Count == 0) {
                return (TopupApplyStatus.Applied, bankDeposit);
            }

            // Hash mismatch — state changed while mod was inactive.
            if (ComputeHash(slots) != compartment.Hash) {
                foreach (var e in compartment.Entries) {
                    bankDeposit.Add(new BankEntry { CurrencyKey = e.CurrencyKey, AssetId = e.AssetId, Amount = e.Excess });
                }
                return (TopupApplyStatus.HashMismatch, bankDeposit);
            }

            // Validate all slot indices before touching anything.
            bool allValid = true;
            foreach (var entry in compartment.Entries) {
                if (!entry.SlotIndex.HasValue
                        || entry.SlotIndex.Value < 0
                        || entry.SlotIndex.Value >= slots.Count
                        || slots[entry.SlotIndex.Value].AssetId != entry.AssetId) {
                    allValid = false;
                    break;
                }
            }
            if (!allValid) {
                foreach (var e in compartment.Entries) {
                    bankDeposit.Add(new BankEntry { CurrencyKey = e.CurrencyKey, AssetId = e.AssetId, Amount = e.Excess });
                }
                return (TopupApplyStatus.LayoutChanged, bankDeposit);
            }

            // Restore and verify each slot.
            foreach (var entry in compartment.Entries) {
                var slot = slots[entry.SlotIndex.Value];
                int expected = entry.VanillaAmount + entry.Excess;
                slot.Amount += entry.Excess;
                // Safeguard: if the restored amount is below expected, recover the deficit.
                int shortfall = expected - slot.Amount;
                if (shortfall > 0) {
                    bankDeposit.Add(new BankEntry { CurrencyKey = entry.CurrencyKey, AssetId = entry.AssetId, Amount = shortfall });
                }
                slots[entry.SlotIndex.Value] = slot;
            }
            return (TopupApplyStatus.Applied, bankDeposit);
        }

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
