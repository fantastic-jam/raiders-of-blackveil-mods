using System;
using System.Collections.Generic;
using System.Reflection;
using HandyPurse.Bank;
using HarmonyLib;
using RR;
using RR.Backend;
using RR.Game;
using RR.Game.Items;
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

        private static bool IsHost => NetworkManager.Instance?.NetworkRunner?.IsServer ?? true;

        private static FieldInfo _itemsArrayField;
        private static MethodInfo _savePlayerGameStatesMethod;
        private static MethodInfo _loadPlayerGameStateMethod;
        private static MethodInfo _savePlayerGameStateLocallyAsyncMethod;

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

            // Optional — only affects auto-pickup merge; vanilla fallback is safe.
            _itemsArrayField = AccessTools.Field(typeof(InventorySyncedItems), "_itemsArray");
            if (_itemsArrayField == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find InventorySyncedItems._itemsArray — auto-pickup merge will use vanilla stack caps.");
            } else {
                var mergeToInventoryMethod = AccessTools.Method(typeof(InventorySyncedItems), nameof(InventorySyncedItems.MergeToInventory));
                harmony.Patch(mergeToInventoryMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(MergeToInventoryPrefix))));
            }

            // Vault save hook — host only, fires for all players' states at once.
            _savePlayerGameStatesMethod = AccessTools.Method(typeof(BackendManager), "SavePlayerGameStates");
            if (_savePlayerGameStatesMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find BackendManager.SavePlayerGameStates — vault save unavailable.");
            } else {
                harmony.Patch(_savePlayerGameStatesMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(SavePlayerGameStatesPrefix))));
            }

            // Vault load hook — host only, wraps callback to apply topup before inventory initialises.
            _loadPlayerGameStateMethod = AccessTools.Method(typeof(BackendManager), "LoadPlayerGameState");
            if (_loadPlayerGameStateMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find BackendManager.LoadPlayerGameState — vault restore unavailable.");
            } else {
                harmony.Patch(_loadPlayerGameStateMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(LoadPlayerGameStatePrefix))));
            }

            // Local save hook — clamps currencies in the local snapshot so it stays in sync with
            // the cloud save (which is clamped by SavePlayerGameStatesPrefix above).
            _savePlayerGameStateLocallyAsyncMethod = AccessTools.Method(typeof(PlayerProfile), "SavePlayerGameStateLocallyAsync");
            if (_savePlayerGameStateLocallyAsyncMethod == null) {
                HandyPurseMod.PublicLogger.LogWarning("HandyPurse: Could not find PlayerProfile.SavePlayerGameStateLocallyAsync — local save will diverge from cloud save.");
            } else {
                harmony.Patch(_savePlayerGameStateLocallyAsyncMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(HandyPursePatch), nameof(SavePlayerGameStateLocallyAsyncPrefix))));
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

        private static void LobbyHUDPageOnActivatePostfix() => BankOrchestrator.ShowPendingPopup();

        private static void SavePlayerGameStatesPrefix(List<PlayerGameState> gameStates) =>
            BankOrchestrator.OnSavePlayerGameStates(gameStates);

        private static void LoadPlayerGameStatePrefix(Guid playerUUID, ref Action<Guid, PlayerGameState> callback) =>
            BankOrchestrator.WrapLoadCallback(playerUUID, ref callback);

        // Keep inventory merge/split logic aware of custom currency limits.
        public static void AmountMaximumPostfix(GenericItemDescriptor __instance, ref int __result) {
            if (Disabled || !IsHost) {
                // Protect existing stacks: don't advertise a max lower than the current amount,
                // otherwise the game clamps them down (InventorySyncedItems lines ~77 and ~156).
                // On clients the host already distributed the correct amounts; we just prevent clamping.
                if (__instance != null && __instance.Amount > __result) {
                    __result = __instance.Amount;
                }
                return;
            }
            var cap = ResolveCap(__instance?.ItemType ?? ItemType.Unknown);
            if (cap > __result) { __result = cap; }
        }

        // Replace the asset stack clamp used when creating network item descriptors.
        public static void CreateItemPrefix(int assetID, ref int amount, bool useStackLimit = true) {
            if (Disabled || !IsHost) { return; }
            if (!useStackLimit) {
                return;
            }

            var itemDatabase = ItemDatabase.Instance;
            if (itemDatabase == null) {
                return;
            }

            var asset = itemDatabase.GetAsset(assetID);
            if (asset == null) {
                return;
            }

            var cap = ResolveCap(asset.ItemType);
            if (cap > 0) {
                amount = Mathf.Min(cap, amount);
            }
        }

        // Ensure the created descriptor advertises the same custom stack maximum.
        public static void CreateItemPostfix(ref NetworkItemDescriptor? __result) {
            if (Disabled || !IsHost) { return; }
            if (!__result.HasValue) {
                return;
            }

            var descriptor = __result.Value;
            var cap = ResolveCap(descriptor.ItemType);
            if (cap <= 0) {
                return;
            }

            if (descriptor.Stack > cap) {
                descriptor.Stack = cap;
            }
            descriptor.StackMaximum = cap;
            __result = descriptor;
        }

        // Replace merge logic for managed item types so it respects the HandyPurse cap
        // instead of the vanilla asset.StackMaximum field.
        public static bool MergeToInventoryPrefix(object __instance, int assetID, ChampionType champion, int amount, InventoryItemChanges changes, ref int __result) {
            if (Disabled || !IsHost) { return true; }
            var asset = ItemDatabase.Instance?.GetAsset(assetID);
            if (asset == null) {
                __result = 0;
                return false;
            }

            var cap = ResolveCap(asset.ItemType);
            if (cap <= 0) {
                return true; // not managed — let the original run
            }

            if (cap <= 1) {
                __result = 0;
                return false;
            }

            if (_itemsArrayField.GetValue(__instance) is not List<GenericItemDescriptor> itemsArray) {
                return true; // fallback to original
            }

            int merged = 0;
            foreach (var item in itemsArray) {
                if (item.AssetID != assetID || !item.IsInInventoryOf(champion)) {
                    continue;
                }
                int space = cap - item.Amount;
                if (space > 0) {
                    int toAdd = Math.Min(amount, space);
                    item.Amount += toAdd;
                    changes.AddChanges(item);
                    merged += toAdd;
                    amount -= toAdd;
                    if (amount <= 0) { break; }
                }
            }
            __result = merged;
            return false; // skip original
        }

        private static bool SavePlayerGameStateLocallyAsyncPrefix(PlayerGameState playerGameState, ref System.Threading.Tasks.Task __result) {
            if (Disabled) { return true; }
            // Skip when offline: cloud save never runs, so topup is never recorded.
            if (NetworkManager.Instance?.IsOffline ?? false) { return true; }
            // If any managed currency exceeds the vanilla cap, skip this local save.
            // Clamping items in-place here mutates the live GenericItemDescriptor references —
            // the same objects the cloud save hook reads moments later — so ProcessSave would
            // see already-clamped amounts and record no topup.
            // The cloud save hook (ProcessSave) will clamp the items itself after recording the
            // excess; the subsequent ValidatePlayerGameState local save will then see correct values.
            if (HasOverCapManagedCurrency(playerGameState)) {
                __result = System.Threading.Tasks.Task.CompletedTask;
                return false;
            }
            return true;
        }

        private static bool HasOverCapManagedCurrency(PlayerGameState state) {
            if (state == null) { return false; }
            var db = ItemDatabase.Instance;
            if (db == null) { return false; }
            return AnyOverCap(db, state.InventoryCommonData?.Items)
                || AnyOverCap(db, state.InventoryChampionData);
        }

        private static bool AnyOverCap(ItemDatabase db, System.Collections.Generic.List<InventoryChampionBackendData> champions) {
            if (champions == null) { return false; }
            foreach (var c in champions) {
                if (AnyOverCap(db, c?.Items)) { return true; }
            }
            return false;
        }

        private static bool AnyOverCap(ItemDatabase db, System.Collections.Generic.List<GenericItemDescriptor> items) {
            if (items == null) { return false; }
            foreach (var item in items) {
                if (ResolveCap(item.ItemType) <= 0) { continue; }
                var asset = db.GetAsset(item.AssetID);
                if (asset != null && item.Amount > asset.StackMaximum) { return true; }
            }
            return false;
        }

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
