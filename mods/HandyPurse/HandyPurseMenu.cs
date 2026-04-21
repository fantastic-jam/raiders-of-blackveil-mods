using System;
using System.Collections.Generic;
using HandyPurse.Bank;
using HandyPurse.Patch;
using HarmonyLib;
using RR;
using RR.Game;
using RR.Game.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace HandyPurse {
    internal static class HandyPurseMenu {
        private static System.Reflection.FieldInfo _syncedItemsField;
        private static System.Reflection.MethodInfo _forceItemStackByIDMethod;

        internal static void Open(VisualElement container, bool isInGameMenu) {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingTop = 16;
            scroll.style.paddingLeft = scroll.style.paddingRight = 28;
            scroll.style.paddingBottom = 20;

            BuildBankSection(scroll);

            if (!HandyPursePatch.Disabled) {
                BuildUninstallSection(scroll);
            }

            container.Add(scroll);
        }

        internal static void Close() { }

        // ── Bank section ──────────────────────────────────────────────────

        private static void BuildBankSection(VisualElement parent) {
            var grayText = new Color(0.65f, 0.65f, 0.65f, 1f);

            var header = new Label { text = "Bank" };
            header.style.color = Color.white;
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 8;
            parent.Add(header);

            var bank = PurseBank.LoadBank();
            var balanceText = BuildBalanceSummary(bank);

            var balanceLabel = new Label { text = $"Balance: {balanceText}" };
            balanceLabel.style.color = grayText;
            balanceLabel.style.fontSize = 12;
            balanceLabel.style.whiteSpace = WhiteSpace.Normal;
            balanceLabel.style.marginBottom = 4;
            parent.Add(balanceLabel);

            var topup = PurseBank.LoadTopup();
            if (topup.Compartments.Count > 0) {
                var topupText = BuildTopupSummary(topup);
                var topupLabel = new Label { text = $"Topup pending: {topupText}" };
                topupLabel.style.color = new Color(0.85f, 0.75f, 0.35f, 1f);
                topupLabel.style.fontSize = 12;
                topupLabel.style.whiteSpace = WhiteSpace.Normal;
                topupLabel.style.marginBottom = 4;
                parent.Add(topupLabel);
            }

            bool hasBalance = bank.Entries.Count > 0 && HasPositiveBalance(bank);
            bool inLobby = GameManager.Instance?.State == GameManager.GameState.Lobby;
            var inventory = PlayerManager.Instance?.LocalPlayer?.Inventory;
            bool hasAuthority = inventory?.HasStateAuthority ?? false;

            var dropStatus = new Label();
            dropStatus.style.fontSize = 12;
            dropStatus.style.whiteSpace = WhiteSpace.Normal;
            dropStatus.style.marginBottom = 4;
            dropStatus.style.display = DisplayStyle.None;
            parent.Add(dropStatus);

            var dropBtn = new Button(() => OnDropBank(dropStatus, bank)) {
                text = "Drop bank to floor"
            };
            dropBtn.style.width = 180;
            dropBtn.style.marginBottom = 20;
            dropBtn.SetEnabled(inLobby && hasAuthority && hasBalance);
            parent.Add(dropBtn);
        }

        private static void OnDropBank(Label status, BankData bank) {
            var (ok, msg) = RunBankDrop(bank);
            status.style.display = DisplayStyle.Flex;
            status.style.color = ok
                ? new Color(0.35f, 0.85f, 0.35f, 1f)
                : new Color(0.9f, 0.35f, 0.35f, 1f);
            status.text = msg;
        }

        private static (bool ok, string message) RunBankDrop(BankData _) {
            var inventory = PlayerManager.Instance?.LocalPlayer?.Inventory;
            if (inventory == null) {
                return (false, "No inventory found.");
            }
            if (!inventory.HasStateAuthority) {
                return (false, "You must be solo or the session host to use this.");
            }

            var db = ItemDatabase.Instance;
            if (db == null) {
                return (false, "ItemDatabase not available.");
            }

            var bank = PurseBank.LoadBank();
            if (bank.Entries.Count == 0 || !HasPositiveBalance(bank)) {
                return (false, "Bank is empty.");
            }

            var playerFilter = inventory.OwnerPlayer.PlayerFilter;
            int stacksDropped = 0;

            foreach (var entry in bank.Entries) {
                if (entry.Amount <= 0) {
                    continue;
                }
                var asset = db.GetAsset(entry.AssetId);
                if (asset == null) {
                    HandyPurseMod.PublicLogger.LogWarning(
                        $"HandyPurse: bank drop — asset {entry.AssetId} not found, skipping.");
                    continue;
                }

                int vanillaMax = asset.StackMaximum;
                int remaining = entry.Amount;
                while (remaining > 0) {
                    int amount = Math.Min(remaining, vanillaMax);
                    var descriptor = db.CreateItem(entry.AssetId, amount);
                    if (descriptor.HasValue) {
                        inventory.DropNewItemToGroundHost(
                            descriptor.Value, playerFilter, useRandomRange: true, forceDropStartPos: null);
                        stacksDropped++;
                    }
                    remaining -= amount;
                }
            }

            PurseBank.TryClearBank();
            return (true,
                $"Dropped {stacksDropped} stack{(stacksDropped != 1 ? "s" : "")} at your feet. " +
                "Pick them up to restore your funds.");
        }

        // ── Uninstall section ─────────────────────────────────────────────

        private static void BuildUninstallSection(VisualElement parent) {
            var grayText = new Color(0.65f, 0.65f, 0.65f, 1f);

            var header = new Label { text = "Uninstall Preparation" };
            header.style.color = Color.white;
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            parent.Add(header);

            var desc = new Label {
                text =
                    "Checks all inventory compartments for currencies above vanilla stack limits and drops the excess " +
                    "as vanilla-sized pickups at your feet, then disables HandyPurse. " +
                    "Pick the stacks back up \u2014 they will merge normally within vanilla limits. " +
                    "Safe to uninstall once done.\n\n" +
                    "Vanilla limits: Scrap 3,000 \u00b7 Black Coin 200 \u00b7 Crystals 200\n\n" +
                    "Must be used from the lobby (not during a run). " +
                    "Solo or session host only."
            };
            desc.style.color = grayText;
            desc.style.fontSize = 12;
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginBottom = 16;
            parent.Add(desc);

            var status = new Label();
            status.style.fontSize = 12;
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginBottom = 10;
            status.style.display = DisplayStyle.None;
            parent.Add(status);

            bool inLobby = GameManager.Instance?.State == GameManager.GameState.Lobby;
            var btn = new Button(() => OnUninstallClick(status)) { text = "Prepare for Uninstall" };
            btn.style.width = 210;
            btn.SetEnabled(inLobby);
            parent.Add(btn);
        }

        private static void OnUninstallClick(Label status) {
            var (ok, msg) = RunUninstallPrep();
            status.style.display = DisplayStyle.Flex;
            status.style.color = ok
                ? new Color(0.35f, 0.85f, 0.35f, 1f)
                : new Color(0.9f, 0.35f, 0.35f, 1f);
            status.text = msg;
        }

        // ── Uninstall preparation logic ───────────────────────────────────

        private static (bool ok, string message) RunUninstallPrep() {
            var inventory = PlayerManager.Instance?.LocalPlayer?.Inventory;
            if (inventory == null) {
                return (false, "No inventory found.");
            }
            if (!inventory.HasStateAuthority) {
                return (false, "You must be solo or the session host to use this.");
            }

            if (!TryResolveReflection(out var syncedItemsField, out var forceStackMethod)) {
                return (false, "Reflection failed \u2014 game may have been updated. Please report a bug.");
            }

            var db = ItemDatabase.Instance;
            if (db == null) {
                return (false, "ItemDatabase not available.");
            }

            var syncedItems = syncedItemsField.GetValue(inventory);

            var overCap = new List<GenericItemDescriptor>();
            for (var tab = ItemTab.Tab0; tab <= ItemTab.Tab5; tab++) {
                foreach (var item in inventory.GetItemsOnTab(tab).ToArray()) {
                    var g = item.AsGenericItem;
                    if (g == null) { continue; }
                    var asset = db.GetAsset(g.AssetID);
                    if (asset != null && g.Amount > asset.StackMaximum) {
                        overCap.Add(g);
                    }
                }
            }

            if (overCap.Count == 0) {
                HandyPursePatch.SetDisabled();
                return (true, "All stacks already within vanilla limits. HandyPurse disabled \u2014 safe to uninstall.");
            }

            var playerFilter = inventory.OwnerPlayer.PlayerFilter;
            int stacksDropped = 0;

            foreach (var item in overCap) {
                var asset = db.GetAsset(item.AssetID);
                int vanillaMax = asset.StackMaximum;
                int excess = item.Amount - vanillaMax;

                while (excess > 0) {
                    int amount = Math.Min(excess, vanillaMax);
                    var descriptor = db.CreateItem(item.AssetID, amount);
                    if (descriptor.HasValue) {
                        inventory.DropNewItemToGroundHost(
                            descriptor.Value, playerFilter, useRandomRange: true, forceDropStartPos: null);
                        stacksDropped++;
                    }
                    excess -= amount;
                }

                forceStackMethod.Invoke(syncedItems, new object[] { item.InventoryID, vanillaMax });
            }

            HandyPursePatch.SetDisabled();
            return (true,
                $"Done. Dropped {stacksDropped} stack{(stacksDropped != 1 ? "s" : "")} at your feet. " +
                "HandyPurse disabled \u2014 pick them up, then uninstall.");
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string BuildBalanceSummary(BankData bank) {
            if (bank.Entries.Count == 0) {
                return "Empty";
            }
            var parts = new List<string>();
            foreach (var e in bank.Entries) {
                if (e.Amount > 0) {
                    parts.Add($"{e.Amount:N0} {e.CurrencyKey}");
                }
            }
            return parts.Count > 0 ? string.Join(" \u00b7 ", parts) : "Empty";
        }

        private static string BuildTopupSummary(TopupData topup) {
            var totals = new Dictionary<string, int>();
            foreach (var compartment in topup.Compartments) {
                foreach (var entry in compartment.Entries) {
                    totals.TryGetValue(entry.CurrencyKey, out var cur);
                    totals[entry.CurrencyKey] = cur + entry.Excess;
                }
            }
            var parts = new List<string>();
            foreach (var kv in totals) {
                if (kv.Value > 0) {
                    parts.Add($"{kv.Value:N0} {kv.Key}");
                }
            }
            return string.Join(" \u00b7 ", parts);
        }

        private static bool HasPositiveBalance(BankData bank) {
            foreach (var e in bank.Entries) {
                if (e.Amount > 0) {
                    return true;
                }
            }
            return false;
        }

        private static bool TryResolveReflection(
                out System.Reflection.FieldInfo syncedItemsField,
                out System.Reflection.MethodInfo forceStackMethod) {
            _syncedItemsField ??= AccessTools.Field(typeof(Inventory), "_syncedItems");
            _forceItemStackByIDMethod ??= AccessTools.Method(typeof(InventorySyncedItems), "ForceItemStackByID");
            syncedItemsField = _syncedItemsField;
            forceStackMethod = _forceItemStackByIDMethod;
            return syncedItemsField != null && forceStackMethod != null;
        }
    }
}
