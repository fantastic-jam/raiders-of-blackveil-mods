# Photon Fusion RPC Inventory

All RPCs use `RpcChannel.Reliable` (no Unreliable RPCs exist in the codebase).

## Source/Target Patterns

| Pattern | Count | Use case |
|---|---|---|
| `StateAuthority → All` | 53 | Authoritative broadcasts — scene loads, game state, VFX, sound, cutscenes |
| `All → StateAuthority` | 47 | Client→host requests needing server validation — pickups, purchases, inventory |
| `InputAuthority → StateAuthority` | 8 | Owner→host inputs — aiming offsets, perk collection |
| `StateAuthority → InputAuthority` | 10 | Host→owner private feedback — action confirmations, cancels, UI pushes |
| `All → All` | 4 | Cosmetic/UI events everyone broadcasts — char change, shrine selection, lobby ready |

---

## AudioManager
`game-src/AudioManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_PlaySoundOnClients(RPCActivatedSounds soundID)` | StateAuthority → All | Plays a named game event sound (`BuffBallEvent_Activated/Won/Lost`) on all clients |

---

## LevelLoadingHandler
`game-src/LevelLoadingHandler.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_StartLobbySceneLoad(SceneRef sceneToLoad, string sceneName, RpcInfo info)` | StateAuthority → All | Load lobby scene on all clients |
| `RPC_StartSceneLoad(SceneRef sceneToLoad, string sceneName, RpcInfo info)` | StateAuthority → All | Load dungeon scene on all clients; fades SFX, shows loading HUD |
| `RPC_Handle_SceneObjectLoaded(string objName, int client, RpcInfo info)` | All → StateAuthority | Client reports a scene object finished loading; host checks if all clients are ready |

---

## AppManager
`game-src/RR/AppManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_IOStatusIndicatorAll(GameIOStatus indicator, bool display)` | StateAuthority → All | Shows/hides the network IO status indicator on all clients |

---

## GameManager
`game-src/RR/GameManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_Close_Lobby(RpcInfo info)` | StateAuthority → All | Calls `LobbyManager.OnSceneUnload()` on all clients |
| `RPC_Handle_StartGame(RpcInfo info)` | StateAuthority → All | Transitions game state to `Dungeon` |
| `RPC_OnQuitToMenu()` | StateAuthority → All | Stops biome audio, deletes biome prefabs; host sends end-metrics and shuts down runner |
| `RPC_Handle_ReturnToLobby(bool runIsWin, bool isFromEndScreen, RpcInfo info)` | StateAuthority → All | Unloads dungeon manager, transitions to `Lobby` state |
| `RPC_SceneLoaded(int sceneIdx, RpcInfo info)` | StateAuthority → All | Notifies all clients a specific scene (by build index) has loaded |
| `RPC_GameEnd(bool victory)` | StateAuthority → All | Sets game state to `EndRun`; shows game-end page on clients |
| `RPC_OnSimplePickup(NetworkObject networkObject, RpcInfo info)` | All → StateAuthority | Host despawns the given `NetworkObject` (simple pickup with no special handling) |
| `RPC_SetGameSoundParameter(GameSoundParameters parameter, float parameterValue, RpcInfo info)` | StateAuthority → All | Calls `AudioManager.SetGameSoundParameter` with an FMOD parameter + value |
| `RPC_ErrorMessageAll(string message)` | StateAuthority → All | Disables player control and shows a fatal error popup on all clients |

---

## PlayerManager
`game-src/RR/PlayerManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_Handle_SetUserData_All(PlayerRef playerRef, string userName, Guid playerProfileUUID, RpcInfo info)` | All → All | Broadcasts username + profile UUID to every peer |
| `RPC_PlayerJoinedPlaySession(PlayerRef playerRef, Guid profileUUID, int charIdx, NetworkBool loadInventoryFromBackend)` | All → StateAuthority | Client notifies host it joined; host initialises and respawns the champion |
| `RPC_SavePlayerGameStateLocally()` | StateAuthority → All | Instructs each client to save their local game state |
| `RPC_ResetInventoryItems(Guid playerUUID)` | All → StateAuthority | Host clears all items from the player's inventory and syncs to backend |
| `RPC_ResetMysticUnlocks(Guid playerUUID)` | All → StateAuthority | Host resets mystic unlock data and syncs to backend |
| `RPC_ResetRebelLeaderProducts(Guid playerUUID)` | All → StateAuthority | Host resets rebel leader progression and syncs to backend |
| `RPC_InitializeStatPerks(Category category, Rarity rarity, int firstPerkID, ushort rnd1-4)` | StateAuthority → All | Clears stat perks for a category+rarity and re-seeds with new random descriptors |

---

## Player
`game-src/RR.Game/Player.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_SetMouseControlled(NetworkBool mouseControlled)` | InputAuthority → StateAuthority | Updates `MouseControlled` networked property on host |
| `RPC_Handle_CharChangeEvent(ChampionType championType, RpcInfo info)` | All → All | Fires character-change event on all clients (champion swap visuals/logic) |

---

## Inventory
`game-src/RR.Game/Inventory.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_DropAllSmugglerItems_Host(Vector3 dropFinalPos, Vector3 dropStartPos)` | All → StateAuthority | Drops all smuggler-tab items to ground at given positions |
| `RPC_SetCurrenciesBalanceFromProgression()` | All → StateAuthority | Syncs currency balances from progression data to inventory |
| `RPC_MerchantPageActivated_Host(NetworkBool pageIsActive)` | All → StateAuthority | Notifies host player opened/closed merchant page; enables/disables restock timer |
| `RPC_CheckWornEquipmentOnHost()` | All → StateAuthority | Validates all worn equipment slots; deactivates invalid ones |
| `RPC_MerchantShopRefresh_Host(int assetID, int price)` | All → StateAuthority | Requests a merchant stock reroll for a specific item |
| `RPC_DebugResetMerchantTimeout()` | All → StateAuthority | Debug: resets merchant restock timeout on host |
| `RPC_ForceMerchantStockCheck()` | All → StateAuthority | Forces immediate merchant stock check on host |
| `RPC_DropEquipmentToGround_Host(NetworkEquipmentDescriptor item, Vector3 startPos, Vector3 groundPos, PlayerFilter playerToPickup)` | All → StateAuthority | Spawns equipment pickup, optionally restricted to a specific player |
| `RPC_DropItemToGround_Host(NetworkItemDescriptor item, Vector3 startPos, Vector3 groundPos, PlayerFilter playerToPickup)` | All → StateAuthority | Spawns generic item pickup, optionally restricted to a specific player |
| `RPC_ActivateOrDeactivateWornEquipment(EquipmentClass equipClass, NetworkBool searchForNewItem, int itemToUnequipID)` | All → StateAuthority | Host activates/deactivates a worn equipment slot, optionally searching for replacement |
| `RPC_RunFinished_Client()` | StateAuthority → InputAuthority | Notifies owning client the run finished (triggers end-of-run inventory logic) |
| `RPC_SendChangesToHost(short changeID, InventoryChanges6 changes)` | All → StateAuthority | Sends batch of up to 6 inventory changes to host |
| `RPC_SendChangesToHost(short changeID, InventoryChanges12 changes)` | All → StateAuthority | Sends batch of up to 12 inventory changes to host |
| `RPC_SyncedAddNewItem(NetworkItemDescriptor item, int invID, ItemTab tab)` | StateAuthority → All | Adds a new item to all clients' inventories |
| `RPC_ExecuteItemActionOnHost(NetworkItemDescriptor item, InventoryAction actionType, int actionNumber, NetworkBool checkPickupObject, NetworkObject pickupObject, ItemInventorySlot targetSlot)` | All → StateAuthority | Sends item action request (collect/use/sell) to host for authoritative resolution |
| `RPC_ExecuteEquipmentActionOnHost(NetworkEquipmentDescriptor equipment, InventoryAction actionType, int actionNumber, NetworkBool checkPickupObject, NetworkObject pickupObject, ItemInventorySlot slot)` | All → StateAuthority | Same as above for equipment items |
| `RPC_FinishEquipmentActionCollect_Client(int actionNumber, NetworkEquipmentDescriptor equipment)` | StateAuthority → InputAuthority | Host confirms equipment collect to owning client |
| `RPC_CancelQueuedAction_Client(int actionNumber)` | StateAuthority → InputAuthority | Host cancels a queued action (e.g. pickup disappeared) |
| `RPC_FinishEquipmentActionBuy_Client(int actionNumber, NetworkEquipmentDescriptor equipment)` | StateAuthority → InputAuthority | Host confirms equipment purchase to owning client |
| `RPC_DeleteOwnedItemSync(int itemInventoryID)` | All → StateAuthority | Authoritative delete of an item from inventory by ID |
| `RPC_DeleteOwnedItem_Local(int itemInventoryID)` | StateAuthority → InputAuthority | Host tells owning client to delete item from local view |
| `RPC_SyncStackOfOwnedItem_Local(int itemInventoryID, int newStack)` | StateAuthority → InputAuthority | Host sends updated stack count to owning client |
| `RPC_FinishItemActionActivate_Client(int actionNumber, int itemID, int remainingStack)` | StateAuthority → InputAuthority | Host confirms item use (e.g. potion); client updates local stack |

---

## UnlockedItems
`game-src/RR.Game/UnlockedItems.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_TryMysticUnlockHost()` | All → StateAuthority | Host attempts to combine Mystic Combine tab items; on success calls `RPC_SetMysticUnlockResult` |
| `RPC_SetMysticUnlockResult(CombineResult result, int unlockedAssetID)` | StateAuthority → All | Notifies all clients of combine result and newly unlocked asset ID |
| `RPC_DEVFeature_ResetAllUnlocks()` | All → StateAuthority | Dev: clears all unlocked assets and collected perk recipes |
| `RPC_Temp_InitUnlockedFeatures(UnlockedAssetsArray assetIDs)` | All → StateAuthority | Initialises networked unlocked-assets array on session join |
| `RPC_Temp_InitCollectedPerkRecipes(UnlockedAssetsArray assetIDs)` | InputAuthority → StateAuthority | Initialises networked collected-perk-recipes array on session join |

---

## Character Ability Classes

Each of these five abilities defines the same RPC:
- `game-src/RR.Game.Character/BeatriceFertileSoilAbility.cs`
- `game-src/RR.Game.Character/BeatriceLotusFlowerAbility.cs`
- `game-src/RR.Game.Character/BeatriceManEaterPlantAbility.cs`
- `game-src/RR.Game.Character/BlazeDevastation.cs`
- `game-src/RR.Game.Character/BlazeSunStrike.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_SetAimingOffset(Vector3 aimOffset)` | InputAuthority → StateAuthority | Syncs player aiming direction offset (X/Z) from input-authority client to host for projectile targeting |

---

## AreaCharacterSelector
`game-src/RR.Game.Perk/AreaCharacterSelector.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_StartVFXTelegraph(float scale, float duration)` | StateAuthority → All | Scales and starts the telegraph VFX decal on all clients |

---

## PerkHandler
`game-src/RR.Game.Perk/PerkHandler.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_RemovePerkOnServer(int perkID, bool removeAll)` | All → StateAuthority | Host decrements or fully removes a perk from `NetworkedCollectedPerks` |
| `RPC_CollectPerkOnServer(int perkID)` | InputAuthority → StateAuthority | Owning client requests host to call `CollectPerkOnHost` |
| `RPC_RegisterCollectedPerk(int perkID)` | StateAuthority → All | Host tells all clients to add the perk and activate its functionalities |
| `RPC_RegisterRemovedPerk(int perkID)` | StateAuthority → All | Host tells all clients to remove the perk |
| `RPC_PlayImpactFX(int perkFuncID)` | StateAuthority → All | Plays impact VFX for a specific perk functionality |
| `RPC_CreateAttachedVFX(int perkFuncID)` | StateAuthority → All | Creates/restarts a persistent attached VFX for a perk functionality |
| `RPC_StopAttachedVFX(int perkFuncID)` | StateAuthority → All | Stops a persistent attached VFX for a perk functionality |
| `RPC_ShowQuestCompleted(int perkID)` | StateAuthority → InputAuthority | Shows quest-completion UI notification to owning client |
| `RPC_SyncPercFuncActivations(ActivationChanges changes)` | StateAuthority → InputAuthority | Syncs perk function activation counts to owning client (local-prediction correction) |

---

## ContainerPickup
`game-src/RR.Game.Pickups/ContainerPickup.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_SetToOpened(PlayerFilter playerFilter, bool isEpicAndKeyUsed)` | StateAuthority → All | Marks container as opened on all clients; plays chain-break VFX; unlocks Epic chest achievement if applicable |

---

## SmugglerGamePickup
`game-src/RR.Game.Pickups/SmugglerGamePickup.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_SetExtractedByPlayer_Host(int playerSlotIndex)` | All → StateAuthority | Host flags the given player slot as having used this smuggler pickup |

---

## Revive
`game-src/RR.Game.Stats/Revive.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_ReviveEvent(int reviver1Slot, int reviver2Slot)` | StateAuthority → All | If the local player is one of the revivers, unlocks the "Revive Friend" achievement |

---

## RuntimeStatistics
`game-src/RR.Game.Stats/RuntimeStatistics.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_RegisterExtractedItem(ItemType itemType, int amount)` | All → StateAuthority | Host increments extracted-item statistics counters (coins, glitter, scraps) |

---

## StatsManager
`game-src/RR.Game.Stats/StatsManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_RefreshStatsUI()` | StateAuthority → InputAuthority | Fires `OnStatsChanged` event on owning client to refresh stat display UI |

---

## BloodBaronBrain
`game-src/RR.Level/BloodBaronBrain.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_OnBloodBaronPickup(int playerId, int stallIdx, NetworkObject obj)` | All → StateAuthority | Host processes a Blood Baron stall purchase: deducts health, despawns pickup, starts animation |

---

## DoorManager
`game-src/RR.Level/DoorManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_VoteState(int slotIdx, int selectedIdx, int hoveredIdx, RpcInfo info)` | All → StateAuthority | Host records player door vote + hover; calls `CheckVotes()` for majority check |
| `RPC_FinalizeVotes(int voteWin, RpcInfo info)` | StateAuthority → All | Broadcasts winning door choice and triggers final UI selection |
| `RPC_VoteEnd(RpcInfo info)` | StateAuthority → All | Closes door voting UI and transitions to `Finished` state |

---

## DungeonManager
`game-src/RR.Level/DungeonManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_TriggerLevelExit(OutroManager.OutroReason reason, int param)` | StateAuthority → All | Fades SFX, calls `SceneExit()` on all level subsystems, triggers outro sequence |
| `RPC_DisablePlayerControl(RpcInfo info)` | StateAuthority → All | Disables control on all player champions (used during cutscenes) |
| `RPC_EnablePlayerControl(RpcInfo info)` | StateAuthority → All | Re-enables control on all player champions |
| `RPC_CutsceneStart(NetworkString<_32> cutsceneName, bool lockPlayers, bool allPlayerDead, RpcInfo info)` | StateAuthority → All | Starts a named cutscene on all clients; optionally locks player movement |
| `RPC_CutsceneFinished(RpcInfo info)` | StateAuthority → All | Signals end of current cutscene to all clients |
| `RPC_TriggerAnimAllPlayers(string animName, RpcInfo info)` | StateAuthority → All | Fires named animation trigger on all player champions |
| `RPC_SetGameSoundDangerLevel(DangerLevel dangerLevel, RpcInfo info)` | StateAuthority → All | Calls `AudioManager.SetGameSoundParameter(DangerLevel, ...)` on all clients |
| `RPC_ObjectsCleared(int client, RpcInfo info)` | All → StateAuthority | Client reports it finished clearing level objects; host checks if all clients are done |
| `RPC_OnAllEnemyKilled(int param, RpcInfo info)` | StateAuthority → All | Raises `LevelEvent_AllEnemiesKilled` on all clients; triggers `OnClearArena` stats on host |

---

## IntroManager
`game-src/RR.Level/IntroManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_IntroActivation()` | StateAuthority → All | Activates intro sequence: host spawns players at spawn points, raises `LevelEvent_IntroStarted`, starts cutscenes |

---

## LevelVendorManager
`game-src/RR.Level/LevelVendorManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_OnContainerOpen(int playerId, ContainerPickup containerObj)` | All → StateAuthority | Host validates container open (checks key if chained), spends key item, calls `RPC_SetToOpened` |

---

## LobbyManager
`game-src/RR.Level/LobbyManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_TriggerAnimAllPlayers(string animName, RpcInfo info)` | StateAuthority → All | Fires named animation trigger on all player champions |
| `RPC_TriggerCutsceneRaidStart(RpcInfo info)` | StateAuthority → All | Starts raid-start cutscene sequence on all clients |
| `RPC_Handle_RaidSetupDone(Difficulty difficulty, int dangerLevel, RpcInfo info)` | StateAuthority → All | Broadcasts selected difficulty and danger level; auto-marks server player as ready |
| `RPC_Handle_PlayerReadyEvent(PlayerRef playerId, RpcInfo info)` | All → All | Sets player slot to `Ready` on host; checks if all ready and starts the start timer |
| `RPC_Handle_PlayerUnreadyEvent(PlayerRef playerId, RpcInfo info)` | All → StateAuthority | Sets player slot back to `NotReady` on host |
| `RPC_ShowDemoTrophy(PlayerFilter playerFilter)` | StateAuthority → All | Shows demo trophy GameObject to targeted player only |
| `RPC_ShowDemoRewardPopupAll(PlayerFilter playerFilter)` | StateAuthority → All | Shows demo participation reward popup to targeted player only |
| `RPC_TriggerLevelExit(OutroManager.OutroReason reason, int param)` | StateAuthority → All | Fades audio and triggers lobby exit/outro |

---

## PowerCore
`game-src/RR.Level/PowerCore.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_ImmuneEnd(RpcInfo info)` | StateAuthority → All | Stops immune VFX and resets immune material parameter |
| `RPC_Hit(RpcInfo info)` | StateAuthority → All | Plays hit feedback and hit sound |
| `RPC_ShieldLost(RpcInfo info)` | StateAuthority → All | Stops immune VFX and resets immune material (shield destroyed) |
| `RPC_LifeLost(RpcInfo info)` | StateAuthority → All | Plays alarm feedback, triggers immune VFX + material, plays wave-spawn sound |
| `RPC_FirstLifeLost(RpcInfo info)` | StateAuthority → All | Switches to damaged visual, plays alarm feedback, starts damaged VFX, triggers immune state |
| `RPC_Destroyed(RpcInfo info)` | StateAuthority → All | Shows destroyed visual, stops damage VFX, plays alarm and destruction sound |

---

## RewardManager
`game-src/RR.Level/RewardManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_ClassBonusActivated(Category category, int bonusLevel, int playerSlotId)` | StateAuthority → All | Shows class-level-up notification to the affected player |
| `RPC_OnRewardPickup(int playerId, RewardCategory rewardCategory, int rewardID, bool isLevelEndReward, NetworkObject obj)` | All → StateAuthority | Host applies stat/perk to player, plays pickup sound, despawns object |
| `RPC_OnPerkPickup(int playerId, int perkID, NetworkObject networkObj)` | All → StateAuthority | Host collects perk for player via `PerkHandler.CollectPerkOnHost`, despawns object |
| `RPC_OnPerkPickupSound(int playerId, int perkID)` | StateAuthority → All | Plays perk pickup audio on all clients |
| `RPC_XPUpgradeCall(ChampionXPDescriptor.UpgradeAbilityType upgrade, int playerId)` | All → StateAuthority | Host applies XP ability upgrade, decrements ability points |
| `RPC_PlayXPUpgradeEffect(NetworkObject networkObj)` | StateAuthority → All | Plays XP level-gain particle effect on all clients |
| `RPC_PlayPickupSound(RewardCategory rewardCategory, Rarity rarity, int type, Vector3 position)` | StateAuthority → All | Plays the correct pickup sound for a reward at a world position |

---

## ShopManager
`game-src/RR.Level/ShopManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_OnShopItemPickup(int playerId, StallType type, string stallName, Rarity rarity, RpcInfo info)` | All → StateAuthority | Host checks currency, deducts gold, applies purchase from named stall |
| `RPC_OnShopPlaySound(int playerId, StallType type, Rarity rarity, RpcInfo info)` | StateAuthority → All | Plays appropriate shop purchase sound (reroll, potion, equipment by rarity) |

---

## ShrineHandler
`game-src/RR.Level/ShrineHandler.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_FillPlayerShrineOptions(int slotIndex, SelectableCaregoryAndPerks option1, SelectableCaregoryAndPerks option2)` | StateAuthority → All | Syncs two shrine options for a player slot to all clients and refreshes shrine UI |
| `RPC_OnPickedUpCategory(PlayerRef player, Category category)` | All → All | Sets selected category for player's shrine slot and updates visuals |
| `RPC_OnPickedUpPerk(PlayerRef player, int perkID, int perkIDSecond, NetworkObject obj)` | All → All | Records perk selection for player's shrine slot and despawns shrine object |
| `RPC_RerollCategoryRequested(PlayerRef playerRef)` | All → StateAuthority | Host spends one RerollToken and regenerates both shrine category options |
| `RPC_RerollPerkRequested(PlayerRef playerRef)` | All → StateAuthority | Host spends one RerollToken and regenerates both shrine perk options |

---

## TutorialManager
`game-src/RR.Level/TutorialManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_EnablePlayerControl(RpcInfo info)` | StateAuthority → All | Re-enables operation and full input on all player champions |
| `RPC_TriggerAnimAllPlayers(string animName, RpcInfo info)` | StateAuthority → All | Fires named animation trigger on all player champions |
| `RPC_TriggerLevelExit(OutroManager.OutroReason reason, int param)` | StateAuthority → All | Used at tutorial end — same pattern as `DungeonManager` |

---

## VendingMachineBrain
`game-src/RR.Level/VendingMachineBrain.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_OnGamblingPickup(int playerId)` | All → StateAuthority | Host validates gambling machine purchase: spends black coins, determines win/lose, starts roll animation |
| `RPC_OnRollStart(bool isWin)` | StateAuthority → All | Triggers roll animation (`"Success"` or `"Failure"`) and plays roll start sound on all clients |

---

## NPCSmuggler
`game-src/PlayerProgression/NPCSmuggler.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_VendorExtractInLevel(int playerID, RpcInfo info)` | StateAuthority → All | Marks local player as `_alreadyExtractedInLevel = true` if `playerID` matches them |

---

## NPCVendorClassCompass
`game-src/PlayerProgression/NPCVendorClassCompass.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_PurchaseSubclassUnlock(Guid playerUUID, Guid productUUID, Guid currencyUUID)` | All → StateAuthority | Host unlocks next subclass for player, spending the specified currency |

---

## PlayerProgressionManager
`game-src/RR.PlayerProgression/PlayerProgressionManager.cs`

| Method | Source → Target | Description |
|---|---|---|
| `RPC_PurchaseProduct(Guid playerUUID, Guid productUUID, Guid currencyUUID)` | All → StateAuthority | Host calls `PurchaseProduct` (meta-progression shop purchase) |
| `RPC_Sell(Guid playerUUID, Guid productUUID, Guid currencyUUID)` | All → StateAuthority | Host calls `Sell` (sells product back for currency) |
| `RPC_AddCurrency(Guid playerUUID, Guid currencyUUID, int amount)` | All → StateAuthority | Host directly adds currency to player's progression wallet |
| `RPC_UpdateCurrenciesBalanceFromInventory(Guid playerUUID)` | All → StateAuthority | Host reads inventory currency counts and syncs to progression wallet |
| `RPC_PurchaseProductRebelLeader(Guid playerUUID, Guid productUUID, Guid playerStatRebelLevel)` | All → StateAuthority | Host calls `PurchaseProductRebelLeader` (rebel-leader tier unlock) |
| `RPC_FlagProductAsViewed(Guid playerUUID, Guid productUUID)` | All → StateAuthority | Host flags a shop product as viewed (removes new-item badge) |
| `RPC_ResetMetaProgressionData(Guid playerUUID)` | All → StateAuthority | Host resets player's entire meta-progression and pushes to backend + client |
| `RPC_RequestPlayerDataSync(Guid playerUUID)` | All → StateAuthority | Host forces a backend sync for the specified player |
