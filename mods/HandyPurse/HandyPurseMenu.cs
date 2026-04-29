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
            container.Clear();
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

            var header = new Label { text = HandyPurseMod.t("menu.bank.header") };
            header.style.color = Color.white;
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 8;
            parent.Add(header);

            var bank = PurseBank.LoadBank();
            var balanceText = BuildBalanceSummary(bank);

            var balanceLabel = new Label { text = HandyPurseMod.t("menu.bank.balance", ("balance", balanceText)) };
            balanceLabel.style.color = grayText;
            balanceLabel.style.fontSize = 12;
            balanceLabel.style.whiteSpace = WhiteSpace.Normal;
            balanceLabel.style.marginBottom = 4;
            parent.Add(balanceLabel);

            var topup = PurseBank.LoadTopup();
            if (topup.Compartments.Count > 0) {
                var topupText = BuildTopupSummary(topup);
                var topupLabel = new Label { text = HandyPurseMod.t("menu.bank.topup", ("topup", topupText)) };
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
                text = HandyPurseMod.t("menu.bank.drop_btn")
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
                return (false, HandyPurseMod.t("error.no_inventory"));
            }
            if (!inventory.HasStateAuthority) {
                return (false, HandyPurseMod.t("error.authority_required"));
            }

            var db = ItemDatabase.Instance;
            if (db == null) {
                return (false, HandyPurseMod.t("error.item_db_unavailable"));
            }

            var bank = PurseBank.LoadBank();
            if (bank.Entries.Count == 0 || !HasPositiveBalance(bank)) {
                return (false, HandyPurseMod.t("error.bank_empty"));
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
            return (true, HandyPurseMod.t("menu.bank.drop_success",
                ("stacks", stacksDropped), ("s", stacksDropped != 1 ? "s" : "")));
        }

        // ── Uninstall section ─────────────────────────────────────────────

        private static void BuildUninstallSection(VisualElement parent) {
            var grayText = new Color(0.65f, 0.65f, 0.65f, 1f);

            var header = new Label { text = HandyPurseMod.t("menu.uninstall.header") };
            header.style.color = Color.white;
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            parent.Add(header);

            var desc = new Label { text = HandyPurseMod.t("menu.uninstall.desc") };
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
            var btn = new Button(() => OnUninstallClick(status)) { text = HandyPurseMod.t("menu.uninstall.btn") };
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
                return (false, HandyPurseMod.t("error.no_inventory"));
            }
            if (!inventory.HasStateAuthority) {
                return (false, HandyPurseMod.t("error.authority_required"));
            }

            if (!TryResolveReflection(out var syncedItemsField, out var forceStackMethod)) {
                return (false, HandyPurseMod.t("error.reflection_failed"));
            }

            var db = ItemDatabase.Instance;
            if (db == null) {
                return (false, HandyPurseMod.t("error.item_db_unavailable"));
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
                return (true, HandyPurseMod.t("menu.uninstall.already_clean"));
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
            return (true, HandyPurseMod.t("menu.uninstall.drop_success",
                ("stacks", stacksDropped), ("s", stacksDropped != 1 ? "s" : "")));
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static string BuildBalanceSummary(BankData bank) {
            if (bank.Entries.Count == 0) {
                return HandyPurseMod.t("label.empty");
            }
            var parts = new List<string>();
            foreach (var e in bank.Entries) {
                if (e.Amount > 0) {
                    parts.Add($"{e.Amount:N0} {e.CurrencyKey}");
                }
            }
            return parts.Count > 0 ? string.Join(" \u00b7 ", parts) : HandyPurseMod.t("label.empty");
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
