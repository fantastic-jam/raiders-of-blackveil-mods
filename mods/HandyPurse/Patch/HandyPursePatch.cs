using System;
using System.Collections.Generic;
using System.Reflection;
using HandyPurse.Bank;
using HarmonyLib;
using RR;
using RR.Backend;
using RR.Backend.API.V1.Ingress.Message;
using RR.Game;
using RR.Game.Items;
using RR.Game.Perk;
using RR.UI.Pages;
using RR.UI.Popups;
using RR.UI.UISystem;
using RR.Utility;
using UnityEngine;

namespace HandyPurse.Patch {
    public static class HandyPursePatch {
        internal static bool Disabled { get; private set; }
        internal static void SetDisabled() => Disabled = true;
        internal static void SetEnabled() => Disabled = false;

        // Set when Apply() fails; cleared after the popup fires once.
        internal static bool PendingBreakingChangePopup { get; set; }

        // Set when the game version check fails in strict mode (patches still applied).
        internal static bool PendingVersionMismatchPopup { get; set; }

        // Shared by BankOrchestrator and InventoryOrchestrator.
        internal static bool IsHost => NetworkManager.Instance?.NetworkRunner?.IsServer ?? true;

        // Exposed for InventoryOrchestrator.IsLocalPlayerItem.
        internal static FieldInfo ItemsArrayField => _itemsArrayField;
        internal static FieldInfo SyncedItemsField => _syncedItemsField;

        private static FieldInfo _itemsArrayField;
        private static FieldInfo _syncedItemsField;
        private static MethodInfo _savePlayerGameStatesMethod;
        private static MethodInfo _loadPlayerGameStateMethod;

        // Always register the main menu hook so the breaking-change popup fires even on failure.
        public static void ApplyMenuHook(Harmony harmony) {
            var menuActivateMethod = AccessTools.Method(typeof(MenuStartPage), "OnActivate");
            if (menuActivateMethod != null) {
                harmony.Patch(menuActivateMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(MenuStartPageOnActivatePostfix))));
            }
        }

        // Returns false if a critical patch could not be applied.
        public static bool Apply(Harmony harmony) {
            var amountMaximumGetter = AccessTools.PropertyGetter(typeof(GenericItemDescriptor), nameof(GenericItemDescriptor.AmountMaximum));
            if (amountMaximumGetter == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find GenericItemDescriptor.AmountMaximum — stack protection unavailable.");
                return false;
            }
            harmony.Patch(amountMaximumGetter,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(AmountMaximumPostfix))));

            var createItemMethod = AccessTools.Method(typeof(ItemDatabase), nameof(ItemDatabase.CreateItem));
            if (createItemMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find ItemDatabase.CreateItem — stack protection unavailable.");
                return false;
            }
            harmony.Patch(createItemMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(CreateItemPrefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(CreateItemPostfix))));

            // Independent null-checks — each handle may independently fail on a game update.
            _itemsArrayField = AccessTools.Field(typeof(InventorySyncedItems), "_itemsArray");
            if (_itemsArrayField == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find InventorySyncedItems._itemsArray — auto-pickup merge will use vanilla stack caps.");
            }
            _syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
            if (_syncedItemsField == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find Inventory._syncedItems — stash ownership check falls back to champion type.");
            }
            if (_itemsArrayField != null && _syncedItemsField != null) {
                var mergeToInventoryMethod = AccessTools.Method(typeof(InventorySyncedItems), nameof(InventorySyncedItems.MergeToInventory));
                harmony.Patch(mergeToInventoryMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(MergeToInventoryPrefix))));
            }

            // Optional — splits over-vanilla currency drops into multiple vanilla-sized stacks.
            var dropMethod = AccessTools.Method(typeof(Inventory), nameof(Inventory.DropOwnedItemToGroundLocal));
            if (dropMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find Inventory.DropOwnedItemToGroundLocal — currency drops will not be split.");
            } else {
                harmony.Patch(dropMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(DropOwnedItemToGroundLocalPrefix))));
            }

            // Vault save hook — prefix clamps managed currencies before serialization;
            // finalizer always restores original amounts (runs even if the original throws).
            _savePlayerGameStatesMethod = AccessTools.Method(typeof(PlayerProfile), nameof(PlayerProfile.SavePlayerGameStates));
            if (_savePlayerGameStatesMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find PlayerProfile.SavePlayerGameStates — vault save unavailable.");
            } else {
                harmony.Patch(_savePlayerGameStatesMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(PlayerProfileSavePlayerGameStatesPrefix))),
                    finalizer: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(PlayerProfileSavePlayerGameStatesFinalizer))));
            }

            // Vault load hook — host only, wraps callback to apply topup before inventory initialises.
            _loadPlayerGameStateMethod = AccessTools.Method(typeof(BackendManager), "LoadPlayerGameState");
            if (_loadPlayerGameStateMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find BackendManager.LoadPlayerGameState — vault restore unavailable.");
            } else {
                harmony.Patch(_loadPlayerGameStateMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(LoadPlayerGameStatePrefix))));
            }

            var lobbyHudActivateMethod = AccessTools.Method(typeof(LobbyHUDPage), "OnActivate");
            if (lobbyHudActivateMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find LobbyHUDPage.OnActivate — bank popup deferred to main menu.");
            } else {
                harmony.Patch(lobbyHudActivateMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(LobbyHUDPageOnActivatePostfix))));
            }

            HandyPurseMod.PublicLogger.LogInfo("HandyPurse patches applied.");
            return true;
        }

        private static void MenuStartPageOnActivatePostfix() {
            if (PendingVersionMismatchPopup) {
                PendingVersionMismatchPopup = false;
                UIManager.Instance?.Popup?.ShowCustom(null, new DefaultOKPopup {
                    Title = HandyPurseMod.t("popup.version_mismatch.title"),
                    Text = HandyPurseMod.t("popup.version_mismatch.text", ("version", HandyPurseMod.Version))
                });
                return;
            }

            if (PendingBreakingChangePopup) {
                PendingBreakingChangePopup = false;
                UIManager.Instance?.Popup?.ShowCustom(null, new DefaultOKPopup {
                    Title = HandyPurseMod.t("popup.breaking_change.title"),
                    Text = HandyPurseMod.t("popup.breaking_change.text", ("version", HandyPurseMod.Version))
                });
                return;
            }

            BankOrchestrator.ShowPendingPopup();
        }

        private static void LobbyHUDPageOnActivatePostfix() {
            BankOrchestrator.OnJoinedSession();
            BankOrchestrator.ShowPendingPopup();
        }

        private static bool DropOwnedItemToGroundLocalPrefix(Inventory __instance, ItemDescriptor item, PlayerFilter playerToPickup, bool useRandomRange, Vector3? forceDropStartPos) =>
            InventoryOrchestrator.OnDropOwnedItem(__instance, item, playerToPickup, useRandomRange, forceDropStartPos);

        private static void PlayerProfileSavePlayerGameStatesPrefix(IngressMessagePlayerSaveGameStates requestSave) =>
            BankOrchestrator.OnPlayerProfileSave(requestSave?.data?.player_game_states);

        private static Exception PlayerProfileSavePlayerGameStatesFinalizer(Exception __exception) {
            BankOrchestrator.OnPlayerProfileSaveComplete();
            return __exception;
        }

        private static void LoadPlayerGameStatePrefix(Guid playerUUID, ref Action<Guid, PlayerGameState> callback, bool initiatedByClient = false) =>
            BankOrchestrator.WrapLoadCallback(playerUUID, ref callback, initiatedByClient);

        private static void AmountMaximumPostfix(GenericItemDescriptor __instance, ref int __result) =>
            InventoryOrchestrator.OnAmountMaximum(__instance, ref __result);

        private static void CreateItemPrefix(int assetID, ref int amount, bool useStackLimit = true) =>
            InventoryOrchestrator.OnCreateItemPrefix(assetID, ref amount, useStackLimit);

        private static void CreateItemPostfix(ref NetworkItemDescriptor? __result) =>
            InventoryOrchestrator.OnCreateItemPostfix(ref __result);

        private static bool MergeToInventoryPrefix(object __instance, int assetID, ChampionType champion, int amount, InventoryItemChanges changes, ref int __result) =>
            InventoryOrchestrator.OnMergeToInventory(__instance, assetID, champion, amount, changes, ref __result, _itemsArrayField);

        internal static int ResolveCap(ItemType itemType) {
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
