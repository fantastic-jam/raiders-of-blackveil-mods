using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fusion;
using HarmonyLib;
using RR;
using RR.Game;
using RR.Game.ItemDrop;
using RR.Game.Perk;
using RR.Game.Stats;
using RR.Game.Items;
using RR.Game.Pickups;
using RR.Level;
using RR.UI.Controls.HUD;
using RR.UI.Extensions;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.Utility;
using UnityEngine;

namespace RogueRun.Patch {
    public static class RogueRunPatch {
        private static FieldInfo _syncedItemsField;
        private static FieldInfo _receivedBackendDataFlagsField;
        private static MethodInfo _sendInventoryToClientMethod;
        private static MethodInfo _getRandomizeSceneMethod;
        private static PropertyInfo _levelSceneProp;
        private static FieldInfo _currentLevelTypeField;
        private static FieldInfo _doorPageField;
        private static FieldInfo _doorCardsField;
        private static FieldInfo _doorInfosBackingField;
        private static MethodInfo _addRewardEnumMethod;
        private static PropertyInfo _nextStepOptionsProp;
        private static FieldInfo _progressionField;

        // Returns false if a critical patch could not be applied.
        public static bool Apply(Harmony harmony) {
            // Critical — without these the inventory cannot be safely stripped or restored.
            _syncedItemsField = AccessTools.Field(typeof(Inventory), "_syncedItems");
            if (_syncedItemsField == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find Inventory._syncedItems — strip/restore unavailable.");
                return false;
            }

            _receivedBackendDataFlagsField = AccessTools.Field(typeof(Inventory), "_receivedBackendDataFlags");
            if (_receivedBackendDataFlagsField == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find Inventory._receivedBackendDataFlags — restore unavailable.");
                return false;
            }

            // Optional — used to push correct inventory state to remote clients after strip/restore.
            var sendStreamDef = typeof(PlayerManager).GetMethods(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "SendItemDataStreamToClient" && m.IsGenericMethodDefinition);
            if (sendStreamDef == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find PlayerManager.SendItemDataStreamToClient — client inventory push inactive.");
            } else {
                _sendInventoryToClientMethod = sendStreamDef.MakeGenericMethod(typeof(InventoryChampionBackendData[]));
            }

            // Critical — without these we cannot track dungeon entry/exit.
            var beginLevelMethod = AccessTools.Method(typeof(BackendManager), "EventBeginLevel");
            if (beginLevelMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find BackendManager.EventBeginLevel — run tracking unavailable.");
                return false;
            }
            harmony.Patch(beginLevelMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(EventBeginLevelPostfix))));


            var lobbySceneLoadDoneMethod = AccessTools.Method(typeof(LobbyManager), "OnSceneLoadDone");
            if (lobbySceneLoadDoneMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find LobbyManager.OnSceneLoadDone — inventory restore on lobby entry unavailable.");
                return false;
            }
            harmony.Patch(lobbySceneLoadDoneMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(LobbyOnSceneLoadDonePostfix))));

            // Optional — client-side: when the server pushes inventory data (strip at dungeon entry,
            // restore at lobby return), strip run loot and clear the guard so ReceiveChampionBackendData accepts it.
            var onClientReceivedMethod = AccessTools.Method(typeof(PlayerManager), "OnClientRecievedInventoryChampionData");
            if (onClientReceivedMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find PlayerManager.OnClientRecievedInventoryChampionData — client re-init guard inactive.");
            } else {
                harmony.Patch(onClientReceivedMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(OnClientRecievedInventoryChampionDataPrefix))));
            }

            // Critical — suppress both local-save paths so run loot is never written to disk mid-run.
            // The RPC is suppressed at the send site (server), so clients never receive it.
            // The direct call is used by GameManager.OnShutdown — must be patched separately.
            var saveLocallyRpcMethod = AccessTools.Method(typeof(PlayerManager), "RPC_SavePlayerGameStateLocally");
            if (saveLocallyRpcMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find PlayerManager.RPC_SavePlayerGameStateLocally — local save suppression unavailable.");
                return false;
            }
            harmony.Patch(saveLocallyRpcMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(SavePlayerGameStateLocallyPrefix))));

            var saveLocallyDirectMethod = AccessTools.Method(typeof(PlayerManager), "SavePlayerGameStateLocally");
            if (saveLocallyDirectMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find PlayerManager.SavePlayerGameStateLocally — shutdown save suppression unavailable.");
            } else {
                harmony.Patch(saveLocallyDirectMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(SavePlayerGameStateLocallyPrefix))));
            }

            // Critical — without this, in-run saves would persist loot to the backend.
            var saveMethod = AccessTools.Method(typeof(BackendManager), "SavePlayerGameStates");
            if (saveMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find BackendManager.SavePlayerGameStates — save suppression unavailable.");
                return false;
            }
            harmony.Patch(saveMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(SavePlayerGameStatesPrefix))));

            // Optional — suppress currency drops (scrap, crystals, black coins are useless in RogueRun).
            var dropCurrencyMethod = AccessTools.Method(typeof(ItemDatabase), nameof(ItemDatabase.DropRandomCurrency));
            if (dropCurrencyMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find ItemDatabase.DropRandomCurrency — currency suppression inactive.");
            } else {
                harmony.Patch(dropCurrencyMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(DropRandomCurrencyPrefix))));
            }

            // Optional — replace souvenir/recipe drops with stat flasks in RogueRun.
            // Patch both overloads: simple (enemy drops) and full (chest drops via DropItemForAllPlayers).
            var souvenirPrefix = new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(DropRandomItemPrefix)));
            var dropRandomItemSimple = AccessTools.Method(typeof(ItemDatabase), nameof(ItemDatabase.DropRandomItem),
                new[] { typeof(object), typeof(ItemType), typeof(DropPos), typeof(PlayerFilter) });
            var dropRandomItemFull = AccessTools.Method(typeof(ItemDatabase), nameof(ItemDatabase.DropRandomItem),
                new[] { typeof(object), typeof(ItemType), typeof(object), typeof(object), typeof(object), typeof(DropPos), typeof(PlayerFilter) });
            if (dropRandomItemSimple == null || dropRandomItemFull == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find ItemDatabase.DropRandomItem overload(s) — souvenir replacement may be incomplete.");
            }
            if (dropRandomItemSimple != null) {
                harmony.Patch(dropRandomItemSimple, prefix: souvenirPrefix);
            }

            if (dropRandomItemFull != null) {
                harmony.Patch(dropRandomItemFull, prefix: souvenirPrefix);
            }

            // Optional — boost rarity on all equipment drops.
            var dropRandomEquipmentMethod = AccessTools.Method(typeof(EquipmentDatabase), nameof(EquipmentDatabase.DropRandomEquipment));
            if (dropRandomEquipmentMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find EquipmentDatabase.DropRandomEquipment — rarity boost inactive.");
            } else {
                harmony.Patch(dropRandomEquipmentMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(DropRandomEquipmentPrefix))));
            }

            // Optional — normal rooms get an equipment chest; minibosses drop a perk.
            var activateMethod = AccessTools.Method(typeof(RewardManager), nameof(RewardManager.Activate));
            if (activateMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find RewardManager.Activate — bonus loot inactive.");
            } else {
                harmony.Patch(activateMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(ActivatePostfix))));
            }

            // Optional — enforce per-player perk pickup restriction in RogueRun.
            var perkCardCollectedMethod = AccessTools.Method(typeof(PerkPickup), "OnCardCollected");
            if (perkCardCollectedMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find PerkPickup.OnCardCollected — perk swap prevention inactive.");
            } else {
                harmony.Patch(perkCardCollectedMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(PerkPickupOnCardCollectedPrefix))));
            }

            // Optional — prevent smuggler NPC from spawning at all in RogueRun.
            var activateSmugglerMethod = AccessTools.Method(typeof(LevelVendorManager), nameof(LevelVendorManager.ActivateSmuggler));
            if (activateSmugglerMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find LevelVendorManager.ActivateSmuggler — smuggler spawn suppression inactive.");
            } else {
                harmony.Patch(activateSmugglerMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(ActivateSmugglerPrefix))));
            }

            // Optional — smugglers still open if this is missing; annoying but not harmful.
            var onCardCollectedMethod = AccessTools.Method(typeof(SmugglerGamePickup), "OnCardCollected");
            if (onCardCollectedMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find SmugglerGamePickup.OnCardCollected — smuggler suppression inactive.");
            } else {
                harmony.Patch(onCardCollectedMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(OnCardCollectedPrefix))));
            }

            // Optional — drops revert to vanilla rates if this is missing.
            var testDropChanceMethod = AccessTools.Method(typeof(EnemyDropRuntime), "TestDropChance");
            if (testDropChanceMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find EnemyDropRuntime.TestDropChance — drop rate boost inactive.");
            } else {
                harmony.Patch(testDropChanceMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(TestDropChancePostfix))));
            }

            // Optional — redirect smuggler rooms to mystery rooms.
            _getRandomizeSceneMethod = AccessTools.Method(typeof(LevelProgressionHandler), "GetRandomizeScene",
                new[] { typeof(LevelType), typeof(int) });
            _levelSceneProp = AccessTools.Property(typeof(LevelProgressionHandler), "LevelScene");
            _currentLevelTypeField = AccessTools.Field(typeof(LevelProgressionHandler), "_CurrentLevelType");
            var goToNextLevelMethod = AccessTools.Method(typeof(LevelProgressionHandler), nameof(LevelProgressionHandler.GoToNextLevel));
            if (goToNextLevelMethod == null || _getRandomizeSceneMethod == null || _levelSceneProp == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find LevelProgressionHandler.GoToNextLevel or helpers — smuggler-to-mystery redirect inactive.");
            } else {
                harmony.Patch(goToNextLevelMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(GoToNextLevelPostfix))));
            }

            // Optional — replace Scrap/Glitter with BlackCoin in door reward UI.
            var addRewardMethod = AccessTools.Method(typeof(DoorPreviewCard2), "AddReward",
                new[] { typeof(LevelRewardBase), typeof(int) });
            if (addRewardMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find DoorPreviewCard2.AddReward — door reward UI substitution inactive.");
            } else {
                harmony.Patch(addRewardMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(DoorAddRewardPrefix))));
            }

            // Optional — expand progression so every door option is played (linear run).
            _nextStepOptionsProp = AccessTools.Property(typeof(LevelProgressionHandler), "NextStepOptions");
            _progressionField = AccessTools.Field(typeof(LevelProgressionHandler), "Progression");
            var resetProgressMethod = AccessTools.Method(typeof(LevelProgressionHandler), "ResetProgress");
            var resetProgressRunConfigMethod = AccessTools.Method(typeof(LevelProgressionHandler), "ResetProgressRunConfig");
            if (_nextStepOptionsProp == null || _progressionField == null || resetProgressMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find LevelProgressionHandler.ResetProgress — linear door expansion inactive.");
            } else {
                var flattenPostfix = new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(FlattenProgressionPostfix)));
                harmony.Patch(resetProgressMethod, postfix: flattenPostfix);
                if (resetProgressRunConfigMethod != null) {
                    harmony.Patch(resetProgressRunConfigMethod, postfix: flattenPostfix);
                }
            }

            // Optional — add Perk reward icon to MiniBoss door cards / extract-only after boss.
            _addRewardEnumMethod = addRewardMethod; // reuse from above
            _doorPageField = AccessTools.Field(typeof(DoorManager), "_doorPage");
            _doorCardsField = AccessTools.Field(typeof(GameDoorSelectPage), "_cards");
            _doorInfosBackingField = AccessTools.Field(typeof(DoorManager), "_DoorInfos");
            // After BiomeBoss, force votedIndex=0 (extract) so loop doors are never taken.
            var nextLevelMethod = AccessTools.Method(typeof(GameManager), "NextLevel");
            if (nextLevelMethod == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find GameManager.NextLevel — extract-only door inactive.");
            } else {
                harmony.Patch(nextLevelMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(NextLevelPrefix))));
            }

            var fillUIMethod = AccessTools.Method(typeof(DoorManager), "FillUI");
            if (fillUIMethod == null || _doorPageField == null || _doorCardsField == null || _doorInfosBackingField == null) {
                RogueRunMod.PublicLogger.LogWarning("RogueRun: Could not find DoorManager.FillUI or helpers — MiniBoss perk UI / extract card inactive.");
            } else {
                harmony.Patch(fillUIMethod,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(FillUIPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(RogueRunPatch), nameof(FillUIPostfix))));
            }

            RogueRunMod.PublicLogger.LogInfo("RogueRun patches applied.");
            return true;
        }

        // ── Level entry/exit ───────────────────────────────────────────────

        private static void EventBeginLevelPostfix() {
            if (!RogueRunState.IsActive || RogueRunState.InRun) {
                return;
            }

            RogueRunState.InRun = true;
            RogueRunMod.PublicLogger.LogInfo("RogueRun: dungeon entered — saves suppressed until lobby.");

            if (_syncedItemsField == null || _receivedBackendDataFlagsField == null) {
                return;
            }

            var players = PlayerManager.Instance?.GetPlayers();
            if (players == null || players.Count == 0 || players[0].Inventory == null) {
                return;
            }

            if (!players[0].Inventory.Object.Runner.IsServer) {
                return; // server-authoritative only
            }

            RogueRunState.ClearSnapshot();

            foreach (var player in players) {
                var inv = player.Inventory;
                if (inv == null) { continue; }

                RogueRunState.PreRunSnapshot[player.SlotIndex] = inv.GetChampionBackendData(inv.ChampionType, null);
                StripInventory(inv);

                // Push stripped state to remote client so they appear empty-handed too.
                if (!player.IsLocal) {
                    PushInventoryToClient(player, inv);
                }

                RogueRunMod.PublicLogger.LogInfo($"RogueRun: stripped player {player.SlotIndex} for run.");
            }
        }

        private static void LobbyOnSceneLoadDonePostfix() {
            if (!RogueRunState.IsActive) {
                return;
            }

            var wasInRun = RogueRunState.InRun;
            RogueRunState.InRun = false;

            if (!wasInRun || _syncedItemsField == null || _receivedBackendDataFlagsField == null) {
                return;
            }

            if (RogueRunState.PreRunSnapshot.Count == 0) {
                return;
            }

            var players = PlayerManager.Instance?.GetPlayers();
            if (players == null || players.Count == 0 || players[0].Inventory == null) {
                return;
            }

            if (!players[0].Inventory.Object.Runner.IsServer) {
                return; // server-authoritative only
            }

            RogueRunMod.PublicLogger.LogInfo("RogueRun: lobby loaded — restoring pre-run inventories.");
            foreach (var player in players) {
                var inv = player.Inventory;
                if (inv == null) { continue; }

                if (!RogueRunState.PreRunSnapshot.TryGetValue(player.SlotIndex, out var snapshot)) { continue; }

                StripInventory(inv);
                RestoreInventory(inv, snapshot);

                // Push restored inventory to remote client so their local copy is correct.
                if (!player.IsLocal) {
                    PushInventoryToClient(player, inv);
                }

                RogueRunMod.PublicLogger.LogInfo($"RogueRun: restored player {player.SlotIndex} inventory.");
            }

            RogueRunState.ClearSnapshot();
        }

        // ── Inventory helpers ──────────────────────────────────────────────

        // Client-side prefix: when the server pushes inventory data (stripped at dungeon entry,
        // restored at lobby return), strip any run loot and clear the "already received" flag
        // so ReceiveChampionBackendData accepts the incoming data.
        private static void OnClientRecievedInventoryChampionDataPrefix(PlayerRef playerRef) {
            if (!RogueRunState.IsActive) { return; }

            var player = PlayerManager.Instance?.GetPlayerByRefId(playerRef.PlayerId);
            var inv = player?.Inventory;
            if (inv == null || _syncedItemsField == null || _receivedBackendDataFlagsField == null) { return; }

            StripInventory(inv);

            var currentFlags = (Inventory.ReceivedBackendDataFlag)_receivedBackendDataFlagsField.GetValue(inv);
            var championFlag = inv.GetChampionBackendDataFlag(inv.ChampionType);
            _receivedBackendDataFlagsField.SetValue(inv, currentFlags & ~championFlag);

            RogueRunMod.PublicLogger.LogInfo($"RogueRun: client prepped for inventory re-init (player {playerRef.PlayerId}).");
        }

        // Server pushes current inventory state to a remote client.
        // Called after strip (at dungeon entry) or after restore (at lobby return).
        private static void PushInventoryToClient(Player player, Inventory inv) {
            if (_sendInventoryToClientMethod == null) { return; }
            try {
                var champData = inv.GetChampionBackendData(inv.ChampionType, null);
                _sendInventoryToClientMethod.Invoke(PlayerManager.Instance, new object[] {
                    player.FusionPlayerRef,
                    DataStreamType.PlayerInventoryChampionAll,
                    new InventoryChampionBackendData[] { champData }
                });
            }
            catch (Exception ex) {
                RogueRunMod.PublicLogger.LogWarning($"RogueRun: PushInventoryToClient failed — {ex.Message}");
            }
        }

        private static void StripInventory(Inventory inv) {
            var syncedItems = (InventorySyncedItems)_syncedItemsField.GetValue(inv);
            if (syncedItems == null) {
                return;
            }

            ChampionType? champion = inv.ChampionType;
            syncedItems.DeleteItemsOnTab(ItemTab.Inventory, champion);
            syncedItems.DeleteItemsOnTab(ItemTab.SafePockets, champion);
            syncedItems.DeleteItemsOnTab(ItemTab.Equipped, champion);

            // Re-sync worn equipment state — Equipped tab is now empty so all slots clear
            inv.InitialActivateWornEquipments_Host();
        }

        private static void RestoreInventory(Inventory inv, InventoryChampionBackendData snapshot) {
            var currentFlags = (Inventory.ReceivedBackendDataFlag)_receivedBackendDataFlagsField.GetValue(inv);
            var championFlag = inv.GetChampionBackendDataFlag(inv.ChampionType);
            _receivedBackendDataFlagsField.SetValue(inv, currentFlags & ~championFlag);
            inv.ReceiveChampionBackendData(snapshot);
        }

        // ── Save suppression ───────────────────────────────────────────────

        // Suppress the local-disk save during a run. DungeonManager.LevelExit() broadcasts
        // this RPC to all peers before returning to lobby — without suppression, the stripped
        // inventory is written locally and could be pushed to the backend on a crash/reconnect.
        private static bool SavePlayerGameStateLocallyPrefix() {
            if (!RogueRunState.IsActive || !RogueRunState.InRun) {
                return true;
            }

            RogueRunMod.PublicLogger.LogInfo("RogueRun: local save suppressed (InRun).");
            return false;
        }

        // Suppress backend saves while inside a RogueRun dungeon level.
        // The callback is invoked immediately so the game's flow control doesn't hang.
        private static bool SavePlayerGameStatesPrefix(ref IEnumerator<WaitForSeconds> __result, Action backendRequestCompleted) {
            if (!RogueRunState.IsActive || !RogueRunState.InRun) {
                return true;
            }

            RogueRunMod.PublicLogger.LogInfo("RogueRun: save suppressed.");
            backendRequestCompleted?.Invoke();
            __result = EmptyCoroutine();
            return false;
        }

        private static IEnumerator<WaitForSeconds> EmptyCoroutine() { yield break; }

        // ── Souvenir replacement ───────────────────────────────────────────

        // Souvenirs and perk recipes have no use in RogueRun — drop a stat flask instead.
        private static void DropRandomItemPrefix(ref ItemType itemType) {
            if (!RogueRunState.IsActive) {
                return;
            }

            if (itemType == ItemType.Souvenir || itemType == ItemType.PerkRecipe) {
                itemType = ItemType.StatFlask;
            }
        }

        // ── Currency conversion ────────────────────────────────────────────

        // Scrap and glitter have no use in RogueRun — convert them to black coins.
        // Glitter → BlackCoin 1:1. Scrap → BlackCoin at 200:3000 (rounded up, minimum 1).
        private static bool DropRandomCurrencyPrefix(ref ItemType itemType, ref ValueRange amountRange) {
            if (!RogueRunState.IsActive) {
                return true;
            }

            if (itemType == ItemType.Glitter) {
                itemType = ItemType.BlackCoin;
                return true;
            }

            if (itemType == ItemType.Scrap) {
                itemType = ItemType.BlackCoin;
                amountRange = new ValueRange(
                    Math.Max(1f, (float)Math.Ceiling(amountRange.rangeMin * 200f / 3000f)),
                    Math.Max(1f, (float)Math.Ceiling(amountRange.rangeMax * 200f / 3000f))
                );
                return true;
            }

            return true;
        }

        // ── Rarity boost ──────────────────────────────────────────────────

        // Shift 60% of each rarity tier's probability mass upward — strongly favours Epic/Legendary/Mythic.
        private static void DropRandomEquipmentPrefix(ref RarityModifier rarityModifier) {
            if (!RogueRunState.IsActive) {
                return;
            }

            rarityModifier.RoundingUpChance += 60f;
        }

        // ── Bonus loot ────────────────────────────────────────────────────

        // Normal rooms always drop an equipment chest; minibosses always drop a perk.
        private static void ActivatePostfix(RewardManager __instance) {
            if (!RogueRunState.IsActive) {
                return;
            }

            if (!__instance.Runner.IsServer) {
                return;
            }

            var levelType = GameManager.Instance?.LevelProgressionHandler?.Level?.Type ?? LevelType.None;

            if (levelType == LevelType.Normal) {
                __instance.RewardForAllPlayers(LevelRewardBase.Equipment, levelType);
            } else if (levelType == LevelType.MiniBoss) {
                __instance.RewardForAllPlayers(LevelRewardBase.Perk, levelType);
            }
        }

        // ── Linear full progression ────────────────────────────────────────

        // Flatten Progression so every option at each step becomes its own step.
        // A base-game step with [MiniBoss, Normal, Shop] becomes three consecutive steps.
        private static void FlattenProgressionPostfix(LevelProgressionHandler __instance) {
            if (!RogueRunState.IsActive) {
                return;
            }

            var progression = _progressionField.GetValue(__instance)
                as System.Collections.Generic.List<System.Collections.Generic.List<LevelDescriptor>>;
            if (progression == null) {
                return;
            }

            var flat = new System.Collections.Generic.List<System.Collections.Generic.List<LevelDescriptor>>();
            foreach (var step in progression) {
                foreach (var option in step) {
                    flat.Add(new System.Collections.Generic.List<LevelDescriptor> { option });
                }
            }
            progression.Clear();
            progression.AddRange(flat);

            // Fix up NextStepOptions to point at the new Progression[1]
            if (progression.Count > 1) {
                _nextStepOptionsProp.SetValue(__instance, progression[1]);
            }
            RogueRunMod.PublicLogger.LogInfo($"RogueRun: progression flattened to {progression.Count} steps.");
        }

        // ── Door reward UI ─────────────────────────────────────────────────

        // Replace Scrap and Glitter reward icons with BlackCoin (they're converted at drop time).
        private static bool DoorAddRewardPrefix(ref LevelRewardBase rewardCategory) {
            if (!RogueRunState.IsActive) {
                return true;
            }

            if (rewardCategory == LevelRewardBase.Scrap || rewardCategory == LevelRewardBase.Glitter) {
                rewardCategory = LevelRewardBase.BlackCoin;
            }

            return true;
        }

        // After BiomeBoss, always extract (votedIndex=0) — skip loop door choices entirely.
        private static void NextLevelPrefix(ref int votedIndex) {
            if (!RogueRunState.IsActive) {
                return;
            }

            if (GameManager.Instance?.LevelProgressionHandler?.NextToFinish == true) {
                votedIndex = 0;
            }
        }

        // When NextToFinish, replace the entire door UI with a single "Extract" (Lobby) card.
        private static bool FillUIPrefix(DoorManager __instance) {
            if (!RogueRunState.IsActive) {
                return true;
            }

            if (GameManager.Instance?.LevelProgressionHandler?.NextToFinish != true) {
                return true;
            }

            var doorPage = _doorPageField.GetValue(__instance);
            var page = doorPage?.GetType().GetProperty("Page")?.GetValue(doorPage) as GameDoorSelectPage;
            if (page == null) {
                return true;
            }

            page.Reset(isPostBoss: true);
            page.AddCard(DoorPreviewType.Lobby);
            return false; // skip original FillUI
        }

        // Add a Perk reward icon to MiniBoss door cards (we drop one per player in ActivatePostfix).
        private static void FillUIPostfix(DoorManager __instance) {
            if (!RogueRunState.IsActive || _addRewardEnumMethod == null) {
                return;
            }

            if (_doorPageField == null || _doorCardsField == null || _doorInfosBackingField == null) {
                return;
            }

            var doorPage = _doorPageField.GetValue(__instance);
            var page = doorPage?.GetType().GetProperty("Page")?.GetValue(doorPage);
            if (page == null) {
                return;
            }

            var cards = _doorCardsField.GetValue(page) as System.Collections.IList;
            if (cards == null) {
                return;
            }

            var doorInfos = (DoorManager.DoorInfo[])_doorInfosBackingField.GetValue(__instance);
            if (doorInfos == null) {
                return;
            }

            for (int i = 0; i < doorInfos.Length && i < cards.Count && doorInfos[i].Type != LevelType.None; i++) {
                if (doorInfos[i].Type == LevelType.MiniBoss) {
                    _addRewardEnumMethod.Invoke(cards[i], new object[] { LevelRewardBase.Perk, 1 });
                }
            }
        }

        // ── Perk swap prevention ───────────────────────────────────────────

        // Boss perks are dropped one per player with their PlayerFilter — enforce that filter
        // so players can't steal each other's perks. AnyPlayer drops are unaffected.
        private static bool PerkPickupOnCardCollectedPrefix(PerkPickup __instance, StatsManager statsManager, ref bool __result) {
            if (!RogueRunState.IsActive) {
                return true;
            }

            if (__instance.PlayerFilter == PlayerFilter.AnyPlayer) {
                return true;
            }

            if (statsManager.Player.PlayerFilter != __instance.PlayerFilter) {
                __result = false;
                return false;
            }

            return true;
        }

        // ── Smuggler → Mystery redirect ────────────────────────────────────

        // When the next level would be a Smuggler room, redirect it to a Mystery room.
        // Smuggler shops are useless in RogueRun (nothing to extract), so we repurpose the slot.
        private static void GoToNextLevelPostfix(LevelProgressionHandler __instance, ref string __result) {
            if (!RogueRunState.IsActive) {
                return;
            }

            if (__instance.CurrentLevelType != LevelType.Smuggler) {
                return;
            }

            var mysteryScene = (string)_getRandomizeSceneMethod.Invoke(__instance, new object[] { LevelType.Mystery, 0 });
            __instance.ForceLevelType(LevelType.Mystery);
            _currentLevelTypeField?.SetValue(__instance, LevelType.Mystery);
            _levelSceneProp.SetValue(__instance, mysteryScene);
            __result = mysteryScene;

            UIManager.Instance?.GetHUDPage()?.CornerNotifications?.AddLevelEvent(
                "@Mystery Event",
                "@The smuggler fled — a mystery room awaits.");

            RogueRunMod.PublicLogger.LogInfo("RogueRun: smuggler room redirected to mystery room.");
        }

        // Prevent the smuggler NPC from spawning in RogueRun.
        private static bool ActivateSmugglerPrefix() => !RogueRunState.IsActive;

        // Safety net: if a smuggler card somehow appears in a non-smuggler room, block it silently.
        private static bool OnCardCollectedPrefix(ref bool __result) {
            if (!RogueRunState.IsActive) {
                return true;
            }

            __result = false;
            return false;
        }

        // ── Drop rate boost ────────────────────────────────────────────────

        // One free re-roll on a failed drop check — roughly doubles drop frequency.
        [ThreadStatic] private static bool _rerolling;

        private static void TestDropChancePostfix(EnemyDropRuntime __instance, ref bool __result, EnemyInfo enemyInfo, DifficultyInfo diffInfo) {
            if (!RogueRunState.IsActive || __result || _rerolling) {
                return;
            }

            _rerolling = true;
            try {
                __result = __instance.TestDropChance(in enemyInfo, in diffInfo);
            }
            finally {
                _rerolling = false;
            }
        }
    }
}
