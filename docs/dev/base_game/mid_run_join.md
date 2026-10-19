# Mid-run join / reconnect (vanilla, since 2026-10 update)

Since the 2026-10-19 game update the game natively lets **raid members** (and Phoenix-recoverable party members) reconnect into a run in progress, restoring their XP, perks, health and shrine offer. Anyone else is still refused. JoinAnytime builds on top of this: it leaves returning raid members to vanilla and only pre-joins strangers.

## Admission

Clients now send their profile GUID as the Fusion connection token (`BackendManager` → `NetworkManager.Launch(..., connectionToken: WomboPlayerId.ToByteArray())`).

`NetworkManager.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)` (server):

- `GetPlayers().Count > 2` → refuse
- `!GameManager.IsLobbyNoRaidStartedYet()` → accept only if `NetworkManager.TryGetConnectionTokenPlayerId(token, out guid)` (internal static) and `PlayerManager.IsRaidMember(guid) || PhoenixManager.IsRecoverablePartyMember(guid)`

`PlayerManager.OnPlayerJoined(NetworkRunner, PlayerRef)` (server) re-checks:

1. `PlayerCount >= 3` → `RPC_NotifySA_PlayerKicked(ref, "Server Full")` + `runner.Disconnect`
2. `IsRaidInProgress() && !IsAllowedRaidJoiner(runner, ref)` → kick "Raid In Progress"
3. `GameManager.IsLevelTransitionActive` → queued in `_pendingJoins`, spawned by `PlayerManager.FlushPendingJoins()` from `DungeonManager/LobbyManager/TutorialManager.OnSceneLoadDone()`
4. else `SpawnPlayerPrefab(runner, ref)` → `runner.Spawn(PlayerPrefab, Vector3.zero, Quaternion.identity, ref)`

```csharp
// private static bool PlayerManager.IsAllowedRaidJoiner(NetworkRunner runner, PlayerRef playerRef)
var mi = AccessTools.Method(typeof(PlayerManager), "IsAllowedRaidJoiner",
    new[] { typeof(NetworkRunner), typeof(PlayerRef) });
var isAllowed = AccessTools.MethodDelegate<Func<NetworkRunner, PlayerRef, bool>>(mi);
```

`PlayerManager.RaidPlayersInfo` (`List<PlayerDisplayInfo>`) is filled in `PlayerManager.RunStart()` from `_activePlayers` and cleared in `RunEnd()` (which also clears `_pendingJoins`). Players spawned later (e.g. JoinAnytime promotions) are **not** added.

`GameManager.IsLevelTransitionActive => IsLevelTransitionStarted || IsLevelClosing`. `IsLevelClosing` is set in `DungeonManager.RPC_TriggerLevelExit` (server); both flags are reset in `OnSceneLoadDone()`.

## State restore on reconnect

- `PlayerManager.OnPlayerLeft` (server) now calls `player.CaptureLiveExitStateForReconnect()` (→ `PhoenixManager.SetLiveExitState`) and `RewardManager.DespawnRewardObjectsForPlayer(player)` before despawning. A leaving player with no `Player` object (or in `_pendingJoins`) is handled gracefully.
- `PlayerManager.RPC_NotifySA_PlayerJoinedPlaySession` calls `PhoenixManager.FlagReconnectRestore(profileUUID)` when `IsInActiveRun` (no-op unless the GUID is in the Phoenix saved party) and spawns the champion next to the host's champion (`InitAndRespawnChampion(idx, shouldRespawn, spawnPosition)`).
- `DungeonManager.Handle_GameEventPlayerCharacterSpawned` (raised from `Player.SetPlayableCharacter`) calls `InitPlayerCharacterAtSpawnPoint(player, onlyTeleport:false, skipTeleport)`; on the server, `PhoenixManager.TryConsumeReconnectRestore` → applies saved perks / XP (`NetworkChampionBase.SetXPForRecovery`) / health and sets `Player.IsReconnectedCurrentLevel`.
- Client side, if the local champion spawns while `DungeonManager` is not yet `Initialized` during an active run, `RunLateJoinClientInit()` runs: HUD/camera setup, `DoorManager.CatchUpLateJoin()`, then `ShrineHandler.RPC_RequestLateJoinCatchUp(ref)` and `RewardManager.RPC_RequestPerkPlusLateJoinCatchUp(ref)`.

## Shrine changes relevant to late joiners

- `ShrineHandler.Instance` no longer exists — use `RewardManager.Instance.ShrineHandler`.
- `ShrineHandler._perPlayerData` is field-initialised with 3 `PerPlayerShrineData` entries; `SceneInit()` re-creates or `Reset()`s them; `RunStart(TestConfigRun = null)` only sets `_state = NotInitializedForRun`.
- The offer is generated on the host (`PerPlayerShrineData.Activate(NetworkRunner, Percentage[], Percentage)` → `GenerateOfferAndSpawnItem`) and carried by the networked `ShrineItem.Offer`; the client `ShrineItem.Render()` only reads that networked offer. Clients no longer need local `ShrineHandler` state.
- `ShrineHandler.CatchUpLateJoin(Player)` (host, via the RPC above) generates or restores an offer for a late joiner while the shrine phase is `Active` and `RewardManager.IsLateJoinCatchUpAllowed`.

## Other related changes

- `NetworkChampionBase.AddXP/SetXP` now calls `UpdateToLevelOnHost(XPLevel)`, so stat increments per level are applied immediately.
- `DungeonManager.LevelExit` has a 5 s failsafe: the exit proceeds even if not every peer sent `RPC_ObjectsCleared`.
- `LevelManager.InitPlayerCharacterAtSpawnPoint(Player, bool onlyTeleport = false, bool skipTeleport = false)` only teleports on the server; elsewhere it re-places a champion that is off the navmesh.
- `DoorManager.Render()` null-checks `_doorPage` in `Activated`, but `FinalizeVotes` (`UpdateFriendState`) and the new `RaidMapActivated` state dereference `PlayerManager.LocalPlayer.SlotIndex`, once the HUD page is resolved (`TryPrepareUI()`), so they NPE on a client with no `LocalPlayer`. Note `RaidMapActivated` itself calls `TryPrepareUI()` from `Render()`, so a guard that skips `Render()` while `_doorPage == null` would block raid-map voting UI from ever initialising.
