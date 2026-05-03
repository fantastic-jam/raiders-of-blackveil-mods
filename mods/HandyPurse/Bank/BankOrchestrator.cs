using System;
using System.Collections.Generic;
using HandyPurse.Patch;
using RR;
using RR.Game.Items;
using RR.UI.Popups;
using RR.UI.UISystem;

namespace HandyPurse.Bank {
    internal static class BankOrchestrator {
        internal static string PendingPopupText { get; private set; }

        // Stored across PlayerProfile.SavePlayerGameStates prefix → finalizer to restore live amounts.
        private static List<(GenericItemDescriptor item, int originalAmount)> _saveRestore;

        // ── Save hook (PlayerProfile.SavePlayerGameStates prefix) ──────────

        internal static void OnPlayerProfileSave(PlayerGameState[] states) {
            _saveRestore = null;
            if (HandyPursePatch.Disabled || states == null) {
                return;
            }

            var localUUID = PlayerManager.Instance?.LocalPlayer?.ProfileUUID ?? Guid.Empty;
            if (localUUID == Guid.Empty) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: save hook fired but LocalPlayer UUID is empty — topup not recorded.");
                return;
            }

            PlayerGameState localState = null;
            foreach (var s in states) {
                if (s?.PlayerId == localUUID) { localState = s; break; }
            }
            if (localState == null) { return; }

            var db = ItemDatabase.Instance;
            if (db == null) { return; }

            var restoreList = new List<(GenericItemDescriptor, int)>();
            var allCompartmentEntries = new List<(string key, List<TopupEntry> entries)>();

            CollectAndClampCompartment("Common", localState.InventoryCommonData?.Items, db, restoreList, allCompartmentEntries);
            if (localState.InventoryChampionData != null) {
                foreach (var champData in localState.InventoryChampionData) {
                    CollectAndClampCompartment(champData.ChampionType.ToString(), champData?.Items, db, restoreList, allCompartmentEntries);
                }
            }

            bool hasExcess = false;
            foreach (var (_, entries) in allCompartmentEntries) {
                if (entries.Count > 0) { hasExcess = true; break; }
            }

            if (hasExcess) {
                var save = new TopupSave {
                    Timestamp = localState.TimeStamp,
                    Compartments = new List<TopupCompartment>(),
                };
                foreach (var (key, entries) in allCompartmentEntries) {
                    if (entries.Count > 0) {
                        save.Compartments.Add(new TopupCompartment { Key = key, Entries = entries });
                    }
                }
                if (!PurseBank.WriteTopupSave(save)) {
                    HandyPurseMod.PublicLogger.LogError(
                        "HandyPurse: failed to write topup file — excess will not be restored on next load.");
                }
            }

            _saveRestore = restoreList.Count > 0 ? restoreList : null;
        }

        // ── Save hook (PlayerProfile.SavePlayerGameStates finalizer) ───────
        // A Harmony Finalizer always runs regardless of exceptions in the original method,
        // ensuring live amounts are always restored even if the original throws.

        internal static void OnPlayerProfileSaveComplete() {
            if (_saveRestore == null) { return; }
            foreach (var (item, original) in _saveRestore) {
                item.Amount = original;
            }
            _saveRestore = null;
        }

        // ── Load hook (BackendManager.LoadPlayerGameState prefix) ─────────

        internal static void WrapLoadCallback(Guid playerUUID, ref Action<Guid, PlayerGameState> callback, bool initiatedByClient) {
            if (HandyPursePatch.Disabled) {
                return;
            }
            // Skip validation loads (ValidatePlayerGameState passes initiatedByClient=true).
            if (initiatedByClient) {
                return;
            }
            var original = callback;
            callback = (uuid, state) => {
                if (state != null) {
                    var localUUID = PlayerManager.Instance?.LocalPlayer?.ProfileUUID ?? Guid.Empty;
                    if (localUUID != Guid.Empty && localUUID == uuid) {
                        ApplyTopupToState(state);
                    }
                }
                original?.Invoke(uuid, state);
            };
        }

        // ── Join-as-client hook (LobbyHUDPage.OnActivate) ─────────────────

        /// <summary>
        /// Called when the lobby HUD activates. If the local player is not the host and there
        /// are pending topup files, deposits them to the bank (can't restore without state authority)
        /// and deletes the files to prevent duplicate deposits on subsequent lobby visits.
        /// </summary>
        internal static void OnJoinedSession() {
            if (HandyPursePatch.Disabled || HandyPursePatch.IsHost) {
                return;
            }

            var allTopups = PurseBank.GetAllTopupSaves();
            if (allTopups.Count == 0) { return; }

            var allDeposit = new List<BankEntry>();
            foreach (var save in allTopups) {
                foreach (var compartment in save.Compartments) {
                    foreach (var entry in compartment.Entries) {
                        allDeposit.Add(new BankEntry {
                            CurrencyKey = entry.CurrencyKey,
                            AssetId = entry.AssetId,
                            Amount = entry.Excess,
                        });
                    }
                }
            }

            if (allDeposit.Count > 0 && PurseBank.TryDeposit(allDeposit)) {
                foreach (var save in allTopups) {
                    PurseBank.DeleteTopupSave(save.Timestamp);
                }
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: joined as client — topup deposited to bank.");
                AppendPendingPopup(HandyPurseMod.t("popup.topup_clamped_environment"));
            }
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
                Text = text,
            });
        }

        // ── Internals ─────────────────────────────────────────────────────

        private static void ApplyTopupToState(PlayerGameState state) {
            var db = ItemDatabase.Instance;
            if (db == null) { return; }

            var topupSave = PurseBank.FindTopupSave(state.TimeStamp);
            if (topupSave == null) { return; }

            ApplyCompartmentTopup("Common", state.InventoryCommonData?.Items, topupSave);
            if (state.InventoryChampionData != null) {
                foreach (var champData in state.InventoryChampionData) {
                    ApplyCompartmentTopup(champData.ChampionType.ToString(), champData?.Items, topupSave);
                }
            }
        }

        private static void ApplyCompartmentTopup(string compartmentKey, List<GenericItemDescriptor> items, TopupSave topupSave) {
            if (items == null) { return; }

            TopupCompartment compartment = null;
            foreach (var c in topupSave.Compartments) {
                if (c.Key == compartmentKey) { compartment = c; break; }
            }
            if (compartment == null || (compartment.Entries?.Count ?? 0) == 0) { return; }

            var slots = ToSlots(items);
            var unresolved = BankLogic.ApplyTopup(slots, compartment.Entries);

            // Write restored amounts back to live item descriptors.
            for (int i = 0; i < items.Count; i++) {
                items[i].Amount = slots[i].Amount;
            }

            if (unresolved.Count > 0) {
                var deposit = new List<BankEntry>(unresolved.Count);
                foreach (var entry in unresolved) {
                    deposit.Add(new BankEntry { CurrencyKey = entry.CurrencyKey, AssetId = entry.AssetId, Amount = entry.Excess });
                }
                if (PurseBank.TryDeposit(deposit)) {
                    HandyPurseMod.PublicLogger.LogWarning(
                        $"HandyPurse: {unresolved.Count} topup entries could not be restored (slot layout changed) — deposited to bank.");
                    AppendPendingPopup(HandyPurseMod.t("popup.layout_changed"));
                }
            }
        }

        private static void CollectAndClampCompartment(
                string key,
                List<GenericItemDescriptor> items,
                ItemDatabase db,
                List<(GenericItemDescriptor, int)> restoreList,
                List<(string, List<TopupEntry>)> allEntries) {
            if (items == null) { return; }

            var slots = ToSlots(items);
            var entries = BankLogic.ComputeExcess(slots, assetId => db.GetAsset(assetId)?.StackMaximum);

            // Apply clamped amounts back to live item descriptors; record originals for finalizer restore.
            for (int i = 0; i < items.Count; i++) {
                if (items[i].Amount != slots[i].Amount) {
                    restoreList.Add((items[i], items[i].Amount));
                    items[i].Amount = slots[i].Amount;
                }
            }

            allEntries.Add((key, entries));
        }

        private static List<ItemSlot> ToSlots(List<GenericItemDescriptor> items) {
            var slots = new List<ItemSlot>(items.Count);
            foreach (var item in items) {
                slots.Add(new ItemSlot { ItemType = (int)item.ItemType, AssetId = item.AssetID, Amount = item.Amount });
            }
            return slots;
        }

        private static void AppendPendingPopup(string text) {
            if (string.IsNullOrEmpty(PendingPopupText)) {
                PendingPopupText = text;
            } else {
                PendingPopupText += "\n\n" + text;
            }
        }
    }
}
