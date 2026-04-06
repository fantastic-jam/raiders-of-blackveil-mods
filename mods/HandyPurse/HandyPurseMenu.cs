using System;
using System.Collections.Generic;
using HandyPurse.Patch;
using HarmonyLib;
using RR;
using RR.Game;
using RR.Game.Items;
using UnityEngine;
using UnityEngine.UIElements;

namespace HandyPurse {
    /// <summary>
    /// Builds the in-game Mods menu panel for HandyPurse and runs the uninstall-preparation logic.
    /// </summary>
    internal static class HandyPurseMenu {
        private static System.Reflection.FieldInfo _syncedItemsField;
        private static System.Reflection.MethodInfo _forceItemStackByIDMethod;

        internal static void Open(VisualElement container, bool isInGameMenu) {
            var grayText = new Color(0.65f, 0.65f, 0.65f, 1f);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingTop = 16;
            scroll.style.paddingLeft = scroll.style.paddingRight = 28;
            scroll.style.paddingBottom = 20;

            // ── Section header ────────────────────────────────────────────
            var header = new Label { text = "Uninstall Preparation" };
            header.style.color = Color.white;
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 12;
            scroll.Add(header);

            // ── Explanation label ─────────────────────────────────────────
            var desc = new Label {
                text =
                    "Checks your chest for currencies above vanilla stack limits and drops the excess " +
                    "as vanilla-sized pickups at your feet, then disables HandyPurse. " +
                    "Pick the stacks back up \u2014 they will merge normally within vanilla limits. " +
                    "Safe to uninstall once done.\n\n" +
                    "Vanilla limits: Scrap 999 \u00b7 Black Coin 99 \u00b7 Crystals 99\n\n" +
                    "Must be used from the lobby (not during a run). " +
                    "Solo or session host only."
            };
            desc.style.color = grayText;
            desc.style.fontSize = 12;
            desc.style.whiteSpace = WhiteSpace.Normal;
            desc.style.marginBottom = 16;
            scroll.Add(desc);

            // ── Status line (hidden until first click) ────────────────────
            var status = new Label();
            status.style.fontSize = 12;
            status.style.whiteSpace = WhiteSpace.Normal;
            status.style.marginBottom = 10;
            status.style.display = DisplayStyle.None;
            scroll.Add(status);

            // ── Button — disabled outside the lobby ───────────────────────
            bool inLobby = GameManager.Instance?.State == GameManager.GameState.Lobby;
            var btn = new Button(() => OnClick(status)) { text = "Prepare for Uninstall" };
            btn.style.width = 210;
            btn.SetEnabled(inLobby);
            scroll.Add(btn);

            container.Add(scroll);
        }

        internal static void Close() { }

        // ── Button handler ────────────────────────────────────────────────

        private static void OnClick(Label status) {
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

            // Collect over-cap stacks from the stash (Tab0..Tab5).
            // Champion inventory items (Inventory, SafePockets, Equipped) don't hold currency stacks.
            var overCap = new List<GenericItemDescriptor>();
            for (var tab = ItemTab.Tab0; tab <= ItemTab.Tab5; tab++) {
                // GetItemsOnTab returns the shared _tempItemList; copy before the next call.
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

                // Spawn vanilla-sized floor pickups for the excess.
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

                // Clamp the remaining stack to the vanilla maximum.
                forceStackMethod.Invoke(syncedItems, new object[] { item.InventoryID, vanillaMax });
            }

            HandyPursePatch.SetDisabled();
            return (true,
                $"Done. Dropped {stacksDropped} stack{(stacksDropped != 1 ? "s" : "")} at your feet. " +
                "HandyPurse disabled \u2014 pick them up, then uninstall.");
        }

        // ── Reflection helpers ────────────────────────────────────────────

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
