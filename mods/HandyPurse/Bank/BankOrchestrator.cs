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
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: save hook fired but LocalPlayer UUID is empty — topup not recorded.");
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
            // Do NOT check localUUID here — PlayerManager.LocalPlayer is null when LoadPlayerGameState
            // fires at session start. Defer the identity check to callback invocation time.
            var original = callback;
            callback = (uuid, state) => {
                if (state != null) {
                    var localUUID = PlayerManager.Instance?.LocalPlayer?.ProfileUUID ?? Guid.Empty;
                    // If LocalPlayer is still null (very early session start), assume it's our own load.
                    if (localUUID == Guid.Empty || localUUID == uuid) {
                        ApplyTopupToState(state);
                    }
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
                Text = text,
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
            if (items == null) { return; }
            var db = ItemDatabase.Instance;
            if (db == null) { return; }

            var slots = ToSlots(items);

            // Diagnostic: log every managed-currency slot so we can see what the save hook receives.
            foreach (var s in slots) {
                if (!BankLogic.IsManagedCurrency(s.ItemType)) { continue; }
                var asset = db.GetAsset(s.AssetId);
                HandyPurseMod.PublicLogger.LogInfo(
                    $"HandyPurse: ProcessSave({compartmentKey}) slot itemType={s.ItemType} assetId={s.AssetId} amount={s.Amount} vanillaMax={(asset == null ? "null" : asset.StackMaximum.ToString())}.");
            }

            var (entries, hash) = BankLogic.ComputeExcess(slots, assetId => db.GetAsset(assetId)?.StackMaximum);

            for (int i = 0; i < items.Count; i++) {
                items[i].Amount = slots[i].Amount;
            }

            HandyPurseMod.PublicLogger.LogInfo(
                $"HandyPurse: ProcessSave({compartmentKey}): {entries.Count} slot(s) above vanilla cap.");

            var topup = PurseBank.LoadTopup();
            if (entries.Count > 0) {
                var compartment = PurseBank.GetOrCreateCompartment(topup, compartmentKey);
                compartment.Entries.Clear();
                compartment.Entries.AddRange(entries);
                compartment.Hash = hash;
                foreach (var e in entries) {
                    HandyPurseMod.PublicLogger.LogInfo(
                        $"HandyPurse:   recorded {e.CurrencyKey} vanilla={e.VanillaAmount} excess={e.Excess} slot={e.SlotIndex}.");
                }
            } else {
                PurseBank.RemoveCompartment(topup, compartmentKey);
            }
            PurseBank.SaveTopup(topup);
        }

        private static void ApplyTopup(string compartmentKey, List<GenericItemDescriptor> items) {
            if (items == null) { return; }

            var topup = PurseBank.LoadTopup();
            var compartment = PurseBank.FindCompartment(topup, compartmentKey);
            HandyPurseMod.PublicLogger.LogInfo(
                $"HandyPurse: ApplyTopup({compartmentKey}): {(compartment == null ? "no topup found" : $"{compartment.Entries.Count} entr(ies) to restore")}.");
            if (compartment == null || compartment.Entries.Count == 0) { return; }

            var slots = ToSlots(items);

            // Capture pre-restore amounts so the diagnostic log can show before/after.
            var preRestore = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++) { preRestore[i] = slots[i].Amount; }

            var (status, bankDeposit) = BankLogic.ApplyTopup(slots, compartment);
            HandyPurseMod.PublicLogger.LogInfo(
                $"HandyPurse: ApplyTopup({compartmentKey}): status={status} bankDeposit={bankDeposit.Count}.");

            for (int i = 0; i < items.Count; i++) {
                items[i].Amount = slots[i].Amount;
            }

            if (bankDeposit.Count > 0 && PurseBank.TryDeposit(bankDeposit)) {
                switch (status) {
                    case TopupApplyStatus.HashMismatch:
                        HandyPurseMod.PublicLogger.LogWarning(
                            $"HandyPurse: topup mismatch for {compartmentKey} — moved to bank.");
                        AppendPendingPopup(
                            HandyPurseMod.t("popup.topup_mismatch", ("compartment", compartmentKey)));
                        break;
                    case TopupApplyStatus.LayoutChanged:
                        AppendPendingPopup(HandyPurseMod.t("popup.layout_changed"));
                        break;
                    case TopupApplyStatus.Applied:
                        // Safeguard triggered — hash matched but restored amounts did not add up.
                        LogRestoreShortfall(compartmentKey, compartment, slots, preRestore, bankDeposit);
                        AppendPendingPopup(
                            HandyPurseMod.t("popup.restore_shortfall", ("compartment", compartmentKey)));
                        break;
                }
            }

            PurseBank.RemoveCompartment(topup, compartmentKey);
            PurseBank.SaveTopup(topup);
        }

        private static void LogRestoreShortfall(
                string compartmentKey,
                TopupCompartment compartment,
                List<ItemSlot> slots,
                int[] preRestore,
                List<BankEntry> bankDeposit) {
            var sb = new StringBuilder();
            sb.Append("HandyPurse: restore shortfall in '").Append(compartmentKey)
              .Append("' — hash matched but amounts did not add up.\n");
            sb.Append("  storedHash=").Append(compartment.Hash).Append('\n');
            sb.Append("  entries (").Append(compartment.Entries.Count).Append("):\n");
            foreach (var entry in compartment.Entries) {
                int idx = entry.SlotIndex ?? -1;
                int before = idx >= 0 && idx < preRestore.Length ? preRestore[idx] : -1;
                int after = idx >= 0 && idx < slots.Count ? slots[idx].Amount : -1;
                int expected = entry.VanillaAmount + entry.Excess;
                int shortfall = Math.Max(0, expected - after);
                sb.Append("    ").Append(entry.CurrencyKey)
                  .Append(" assetId=").Append(entry.AssetId)
                  .Append(" slot=").Append(idx)
                  .Append(" recordedVanilla=").Append(entry.VanillaAmount)
                  .Append(" excess=").Append(entry.Excess)
                  .Append(" before=").Append(before)
                  .Append(" after=").Append(after)
                  .Append(" expected=").Append(expected)
                  .Append(" shortfall=").Append(shortfall)
                  .Append('\n');
            }
            sb.Append("  deposited to bank (").Append(bankDeposit.Count).Append("):\n");
            foreach (var e in bankDeposit) {
                sb.Append("    ").Append(e.CurrencyKey)
                  .Append(" assetId=").Append(e.AssetId)
                  .Append(" amount=").Append(e.Amount).Append('\n');
            }
            HandyPurseMod.PublicLogger.LogWarning(sb.ToString());
        }

        // ── Helpers ───────────────────────────────────────────────────────

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
