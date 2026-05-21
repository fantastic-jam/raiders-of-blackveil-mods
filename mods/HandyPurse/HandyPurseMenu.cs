using System;
using System.Collections.Generic;
using HandyPurse.Bank;
using RR;
using RR.Game;
using RR.Game.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace HandyPurse {
    internal static class HandyPurseMenu {
        internal static void Open(VisualElement container, bool isInGameMenu) {
            container.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingTop = 16;
            scroll.style.paddingLeft = scroll.style.paddingRight = 28;
            scroll.style.paddingBottom = 20;
            BuildBankSection(scroll);
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
                if (entry.Amount <= 0) { continue; }
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
            return parts.Count > 0 ? string.Join(" · ", parts) : HandyPurseMod.t("label.empty");
        }

        private static bool HasPositiveBalance(BankData bank) {
            foreach (var e in bank.Entries) {
                if (e.Amount > 0) { return true; }
            }
            return false;
        }
    }
}
