using System;
using System.Collections.Generic;
using System.Text;
using HandyPurse.Patch;
using RR;
using RR.Game.Items;
using RR.UI.Popups;
using RR.UI.UISystem;

namespace HandyPurse.Bank {
    internal static class BankOrchestrator {
        internal static string PendingPopupText { get; private set; }

        // ── Save hook (BackendManager.SavePlayerGameStates prefix) ────────

        internal static void OnSavePlayerGameStates(List<PlayerGameState> gameStates) {
            if (HandyPursePatch.Disabled || gameStates == null) {
                return;
            }

            var localUUID = PlayerManager.Instance?.LocalPlayer?.ProfileUUID ?? Guid.Empty;
            if (localUUID == Guid.Empty) {
                return;
            }

            foreach (var state in gameStates) {
                if (state.PlayerId != localUUID) {
                    continue;
                }
                ProcessSave("Common", state.InventoryCommonData?.Items);
                if (state.InventoryChampionData != null) {
                    foreach (var champData in state.InventoryChampionData) {
                        ProcessSave(champData.ChampionType.ToString(), champData?.Items);
                    }
                }
                break;
            }
        }

        // ── Load hook (BackendManager.LoadPlayerGameState prefix) ─────────

        internal static void WrapLoadCallback(Guid playerUUID, ref Action<Guid, PlayerGameState> callback) {
            if (HandyPursePatch.Disabled) {
                return;
            }

            var localUUID = PlayerManager.Instance?.LocalPlayer?.ProfileUUID ?? Guid.Empty;
            if (localUUID == Guid.Empty || playerUUID != localUUID) {
                return;
            }

            var original = callback;
            callback = (uuid, state) => {
                if (state != null) {
                    ApplyTopupToState(state);
                }
                original?.Invoke(uuid, state);
            };
        }

        // ── Popup ─────────────────────────────────────────────────────────

        internal static void ShowPendingPopup() {
            if (string.IsNullOrEmpty(PendingPopupText)) {
                return;
            }

            var text = PendingPopupText;
            PendingPopupText = null;
            UIManager.Instance?.Popup?.ShowCustom(null, new DefaultOKPopup {
                Title = HandyPurseMod.t("popup.funds_banked.title"),
                Text = text
            });
        }

        // ── Internals ─────────────────────────────────────────────────────

        private static void ApplyTopupToState(PlayerGameState state) {
            ApplyTopup("Common", state.InventoryCommonData?.Items);
            if (state.InventoryChampionData != null) {
                foreach (var champData in state.InventoryChampionData) {
                    ApplyTopup(champData.ChampionType.ToString(), champData?.Items);
                }
            }
        }

        private static void ProcessSave(string compartmentKey, List<GenericItemDescriptor> items) {
            if (items == null) {
                return;
            }
            var db = ItemDatabase.Instance;
            if (db == null) {
                return;
            }

            var topup = PurseBank.LoadTopup();
            var compartment = PurseBank.GetOrCreateCompartment(topup, compartmentKey);
            compartment.Entries.Clear();

            for (int i = 0; i < items.Count; i++) {
                var item = items[i];
                if (!IsManagedCurrency(item.ItemType)) {
                    continue;
                }
                var asset = db.GetAsset(item.AssetID);
                if (asset == null || item.Amount <= asset.StackMaximum) {
                    continue;
                }

                int excess = item.Amount - asset.StackMaximum;
                item.Amount = asset.StackMaximum;

                compartment.Entries.Add(new TopupEntry {
                    CurrencyKey = item.ItemType.ToString(),
                    AssetId = item.AssetID,
                    VanillaAmount = asset.StackMaximum,
                    Excess = excess,
                    SlotIndex = i
                });
            }

            if (compartment.Entries.Count > 0) {
                compartment.Hash = ComputeHash(items);
            } else {
                PurseBank.RemoveCompartment(topup, compartmentKey);
            }

            PurseBank.SaveTopup(topup);
        }

        private static void ApplyTopup(string compartmentKey, List<GenericItemDescriptor> items) {
            if (items == null) {
                return;
            }

            var topup = PurseBank.LoadTopup();
            var compartment = PurseBank.FindCompartment(topup, compartmentKey);
            if (compartment == null || compartment.Entries.Count == 0) {
                return;
            }

            var currentHash = ComputeHash(items);
            if (currentHash != compartment.Hash) {
                var mismatchDeposit = new List<BankEntry>();
                foreach (var entry in compartment.Entries) {
                    mismatchDeposit.Add(new BankEntry {
                        CurrencyKey = entry.CurrencyKey,
                        AssetId = entry.AssetId,
                        Amount = entry.Excess
                    });
                }
                if (PurseBank.TryDeposit(mismatchDeposit)) {
                    HandyPurseMod.PublicLogger.LogWarning(
                        $"HandyPurse: topup mismatch for {compartmentKey} — moved to bank.");
                    AppendPendingPopup(
                        HandyPurseMod.t("popup.topup_mismatch", ("compartment", compartmentKey)));
                }
                PurseBank.RemoveCompartment(topup, compartmentKey);
                PurseBank.SaveTopup(topup);
                return;
            }

            // Phase 1 — validate every slot before touching anything.
            bool allValid = true;
            foreach (var entry in compartment.Entries) {
                bool valid = entry.SlotIndex.HasValue
                    && entry.SlotIndex.Value >= 0
                    && entry.SlotIndex.Value < items.Count
                    && items[entry.SlotIndex.Value].AssetID == entry.AssetId;
                if (!valid) {
                    HandyPurseMod.PublicLogger.LogWarning(
                        $"HandyPurse: slot {entry.SlotIndex} mismatch for {entry.CurrencyKey} in {compartmentKey} — banking all.");
                    allValid = false;
                    break;
                }
            }

            if (!allValid) {
                var fullDeposit = new List<BankEntry>();
                foreach (var entry in compartment.Entries) {
                    fullDeposit.Add(new BankEntry { CurrencyKey = entry.CurrencyKey, AssetId = entry.AssetId, Amount = entry.Excess });
                }
                PurseBank.TryDeposit(fullDeposit);
                AppendPendingPopup(HandyPurseMod.t("popup.layout_changed"));
                PurseBank.RemoveCompartment(topup, compartmentKey);
                PurseBank.SaveTopup(topup);
                return;
            }

            // Phase 2 — all slots validated, restore in-place.
            foreach (var entry in compartment.Entries) {
                var slot = items[entry.SlotIndex.Value];
                slot.Amount += entry.Excess;
                HandyPurseMod.PublicLogger.LogInfo(
                    $"HandyPurse: restored {entry.Excess} {entry.CurrencyKey} to slot {entry.SlotIndex} from topup ({compartmentKey}).");
            }

            PurseBank.RemoveCompartment(topup, compartmentKey);
            PurseBank.SaveTopup(topup);
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string ComputeHash(List<GenericItemDescriptor> items) {
            var managed = new List<(int assetId, int amount)>();
            foreach (var item in items) {
                if (IsManagedCurrency(item.ItemType)) {
                    managed.Add((item.AssetID, item.Amount));
                }
            }
            managed.Sort((a, b) => a.assetId.CompareTo(b.assetId));

            var sb = new StringBuilder();
            for (int i = 0; i < managed.Count; i++) {
                if (i > 0) {
                    sb.Append('|');
                }
                sb.Append(managed[i].assetId);
                sb.Append(':');
                sb.Append(managed[i].amount);
            }
            return sb.ToString();
        }

        private static GenericItemDescriptor FindItem(List<GenericItemDescriptor> items, int assetId) {
            foreach (var item in items) {
                if (item.AssetID == assetId) {
                    return item;
                }
            }
            return null;
        }

        private static void AppendPendingPopup(string text) {
            if (string.IsNullOrEmpty(PendingPopupText)) {
                PendingPopupText = text;
            } else {
                PendingPopupText += "\n\n" + text;
            }
        }

        internal static bool IsManagedCurrency(ItemType type) =>
            type == ItemType.Scrap
            || type == ItemType.BlackCoin
            || type == ItemType.BlackBlood
            || type == ItemType.Glitter;

        /// <summary>
        /// Clamps managed currencies in the local-save snapshot to vanilla StackMaximum —
        /// without touching the topup file. The cloud path (<see cref="ProcessSave"/>) handles
        /// topup recording; this keeps the local file in sync so the two saves never diverge.
        /// </summary>
        internal static void ClampForLocalSave(PlayerGameState state) {
            if (state == null) { return; }
            ClampItems(state.InventoryCommonData?.Items);
            if (state.InventoryChampionData != null) {
                foreach (var champData in state.InventoryChampionData) {
                    ClampItems(champData?.Items);
                }
            }
        }

        private static void ClampItems(List<GenericItemDescriptor> items) {
            if (items == null) { return; }
            var db = ItemDatabase.Instance;
            if (db == null) { return; }
            foreach (var item in items) {
                if (!IsManagedCurrency(item.ItemType)) { continue; }
                var asset = db.GetAsset(item.AssetID);
                if (asset == null || item.Amount <= asset.StackMaximum) { continue; }
                item.Amount = asset.StackMaximum;
            }
        }
    }
}
