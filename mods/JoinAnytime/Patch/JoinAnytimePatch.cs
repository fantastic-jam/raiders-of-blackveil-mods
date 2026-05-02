using System.Reflection;
using Fusion;
using Fusion.Sockets;
using HarmonyLib;
using RR;
using RR.Backend.API.V1.Ingress.Message;
using RR.Game.Character;
using RR.Level;
using WildguardModFramework.Network;

namespace JoinAnytime.Patch {
    internal static class JoinAnytimePatch {
        private static Harmony _harmony;
        private static bool _patched;
        internal static bool Disabled;

        private static PropertyInfo _perPlayerDataSlotIndexProp;
        private static FieldInfo _doorPageField;

        internal static bool Init() => true;

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;

            // ── Keep the session joinable ─────────────────────────────────────
            // Block the backend "run started" signal and the LobbyEnd metric so the
            // session stays advertised in the server browser during a run.

            var beginRun = AccessTools.Method(typeof(BackendManager), nameof(BackendManager.PlaySessionBeginRun));
            if (beginRun != null) {
                harmony.Patch(beginRun,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(PlaySessionBeginRunPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: BackendManager.PlaySessionBeginRun not found — session may be hidden from server browser during runs.");
            }

            var sendUpdateEvent = AccessTools.Method(typeof(MetricsManager), nameof(MetricsManager.SendPlaySessionUpdateEvent));
            if (sendUpdateEvent != null) {
                harmony.Patch(sendUpdateEvent,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(SendPlaySessionUpdateEventPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: MetricsManager.SendPlaySessionUpdateEvent not found — session may be hidden from server browser during runs.");
            }

            // ── Accept connections during a run ───────────────────────────────
            // Vanilla refuses connections when IsInActiveRun. Replace with: accept
            // iff GetPlayers().Count + PreJoinerCount < 3.

            var onConnectRequest = AccessTools.Method(typeof(NetworkManager), nameof(NetworkManager.OnConnectRequest));
            if (onConnectRequest != null) {
                harmony.Patch(onConnectRequest,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(OnConnectRequestPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: NetworkManager.OnConnectRequest not found — mid-run joins will be refused by the game.");
            }

            // ── Pre-join intercept ────────────────────────────────────────────
            // Intercept the spawn of the Player network object on join, so a mid-run
            // joiner ends up with no Player → no AddPlayer → no champion spawn → no
            // _activePlayers entry. Stored as a PlayerRef in JoinAnytimeManager.

            var onPlayerJoined = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerJoined));
            if (onPlayerJoined != null) {
                harmony.Patch(onPlayerJoined,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(OnPlayerJoinedPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: PlayerManager.OnPlayerJoined not found — mid-run pre-join inactive.");
            }

            var onPlayerLeft = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerLeft));
            if (onPlayerLeft != null) {
                harmony.Patch(onPlayerLeft,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(OnPlayerLeftPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: PlayerManager.OnPlayerLeft not found — pre-joiner disconnect cleanup inactive.");
            }

            // ── Promotion at start of next-level load ─────────────────────────
            // GameManager.NextLevel runs server-side after the previous room's exit
            // cutscene has finished AND every existing client has reported in via
            // DungeonManager.RPC_ObjectsCleared (so the level-exit gate has already
            // cleared). Spawning Player objects earlier — at OutroManager.Activate or
            // RPC_TriggerLevelExit — adds the pre-joiner to _activePlayers before the
            // gate runs, which then waits forever for an RPC_ObjectsCleared the new
            // half-initialized client never sends, freezing the level transition.
            // NextLevel itself fires only for LevelWin / CheatFinish (per
            // DungeonManager.ProceedWithLevelExit), so no reason check is needed.

            var nextLevel = AccessTools.Method(typeof(GameManager), nameof(GameManager.NextLevel));
            if (nextLevel != null) {
                harmony.Patch(nextLevel,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(NextLevelPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: GameManager.NextLevel not found — pre-joiners will never be promoted.");
            }

            // ── Join averaging ────────────────────────────────────────────────
            // After a promoted joiner's champion spawns, apply floor-averaged XP,
            // ability levels, and random perks so they start roughly on par.

            var afterSpawned = AccessTools.Method(typeof(NetworkChampionBase), nameof(NetworkChampionBase.AfterSpawned));
            if (afterSpawned != null) {
                harmony.Patch(afterSpawned,
                    postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(ChampionAfterSpawnedPostfix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: NetworkChampionBase.AfterSpawned not found — join averaging inactive.");
            }

            // ── Skip shrine spawn for unmodded promoted joiners ───────────────
            // Unmodded joiners never run ShrineHandler.SceneInit() so _perPlayerData
            // is null on their client — spawning a ShrineItem crashes them with
            // ArgumentOutOfRangeException every render frame. Skip Activate() for
            // them server-side and grant a random perk directly instead.
            // Modded joiners get the full shrine lifecycle via RunStart() below.

            var perPlayerDataType = typeof(ShrineHandler).GetNestedType(
                "PerPlayerShrineData", System.Reflection.BindingFlags.NonPublic);
            if (perPlayerDataType != null) {
                _perPlayerDataSlotIndexProp = AccessTools.Property(perPlayerDataType, "SlotIndex");
                var activate = AccessTools.Method(perPlayerDataType, "Activate");
                if (activate != null && _perPlayerDataSlotIndexProp != null) {
                    harmony.Patch(activate,
                        prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(PerPlayerShrineDataActivatePrefix)));
                } else {
                    JoinAnytimeMod.PublicLogger.LogWarning(
                        "JoinAnytime: PerPlayerShrineData.Activate or SlotIndex not found — unmodded promoted joiners will crash on shrine rooms.");
                }
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: ShrineHandler+PerPlayerShrineData not found — unmodded promoted joiners will crash on shrine rooms.");
            }

            // ── Reset ShrineHandler for modded joiners at level exit ──────────
            // RPC_TriggerLevelExit fires on ALL clients. Postfixing it with
            // RunStart() resets _state = NotInitialized so the next SceneInit()
            // populates _perPlayerData on the modded joiner's client.
            // Only fires when a modded pre-joiner is present.

            var triggerLevelExit = AccessTools.Method(typeof(DungeonManager), nameof(DungeonManager.RPC_TriggerLevelExit));
            if (triggerLevelExit != null) {
                harmony.Patch(triggerLevelExit,
                    postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(RpcTriggerLevelExitPostfix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: DungeonManager.RPC_TriggerLevelExit not found — modded joiners will have broken shrine.");
            }

            // ── Guard client-side crashes for modded joiners ──────────────────
            // While spectating (no LocalPlayer), several vanilla methods crash on
            // LocalPlayer being null. Guard them so modded joiners have a clean
            // experience. Unmodded clients are not affected (mod not installed).

            // Skip Render() entirely when _doorPage is null — covers both the
            // DoorState.Activated (_doorPage.IsOpen) and DoorState.FinalizeVotes
            // (UpdateFriendState → _doorPage.Page.SetVotes) crash paths that fire
            // every frame while a modded joiner has no LocalPlayer yet.
            _doorPageField = AccessTools.Field(typeof(DoorManager), "_doorPage");
            if (_doorPageField == null) {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: DoorManager._doorPage not found — Render guard inactive.");
            }

            var doorManagerRender = AccessTools.Method(typeof(DoorManager), nameof(DoorManager.Render));
            if (doorManagerRender != null) {
                harmony.Patch(doorManagerRender,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(DoorManagerRenderPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: DoorManager.Render not found — modded joiners may see door NPEs.");
            }

            // ── Suppress RPC_ClassBonusActivated for unmodded promoted joiners ──
            // Our averaging code calls CollectPerkOnHost which can trigger class-bonus
            // RPCs. The unmodded client crashes in RPC_ClassBonusActivated because
            // GetPlayerBySlot() returns null for the promoted player's slot on their
            // machine. Prefix the RPC on the server so it is never broadcast when it
            // would target an unmodded promoted joiner's slot.
            var rpcClassBonusActivated = AccessTools.Method(typeof(RewardManager), nameof(RewardManager.RPC_ClassBonusActivated));
            if (rpcClassBonusActivated != null) {
                harmony.Patch(rpcClassBonusActivated,
                    prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(RpcClassBonusActivatedPrefix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: RewardManager.RPC_ClassBonusActivated not found — class bonus RPC may crash unmodded promoted joiners.");
            }

            // ── Despawn unchosen perk pickups when unmodded joiner collects one ──
            var rpcOnPerkPickup = AccessTools.Method(typeof(RewardManager), nameof(RewardManager.RPC_OnPerkPickup));
            if (rpcOnPerkPickup != null) {
                harmony.Patch(rpcOnPerkPickup,
                    postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(RpcOnPerkPickupPostfix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: RewardManager.RPC_OnPerkPickup not found — unchosen perk pickups will not be cleaned up.");
            }

            // ── Force spawn-point placement for promoted joiners ─────────────────
            // IntroManager.RPC_IntroActivation iterates GetPlayers() and calls
            // InitPlayerCharacterAtSpawnPoint — but only for players whose champion is
            // already ready. If the promoted joiner's champion spawn RPC hasn't resolved
            // yet when the intro fires, they are skipped and their champion stays at
            // Vector3.zero, showing a grey fog-of-war circle. The postfix re-runs
            // placement for any promoted joiner still in _pendingPlacement.

            var introActivation = AccessTools.Method(typeof(IntroManager), "RPC_IntroActivation");
            if (introActivation != null) {
                harmony.Patch(introActivation,
                    postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(IntroManagerRpcIntroActivationPostfix)));
            } else {
                JoinAnytimeMod.PublicLogger.LogWarning(
                    "JoinAnytime: IntroManager.RPC_IntroActivation not found — promoted joiners may spawn at wrong position.");
            }

            // Subscribe to the JoinAnytime-specific handshake channel.
            // Joining clients with JoinAnytime installed send "joinanytime:present" on join;
            // the host uses this (not WMF's generic isModded flag) to detect mod presence.
            WmfNetwork.Subscribe("joinanytime:present", JoinAnytimeManager.OnJoinAnytimePresentReceived);

            JoinAnytimeMod.PublicLogger.LogInfo("JoinAnytime patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            WmfNetwork.Unsubscribe("joinanytime:present", JoinAnytimeManager.OnJoinAnytimePresentReceived);
            _harmony?.UnpatchSelf();
            _patched = false;
        }

        // ── Patch methods ─────────────────────────────────────────────────────

        private static bool PlaySessionBeginRunPrefix() => Disabled;

        private static bool SendPlaySessionUpdateEventPrefix(IngressMessagePlaySessionUpdateEvent.EventType event_type) =>
            Disabled || JoinAnytimeManager.ShouldSendUpdateEvent(event_type);

        private static bool OnConnectRequestPrefix(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request) {
            if (Disabled) { return true; }
            if (!runner.IsServer) { return true; }
            if (GameManager.Instance == null || PlayerManager.Instance == null) { return true; }
            if (!GameManager.Instance.IsInActiveRun) { return true; }

            // Count active players + pre-joiners. PlayerCount itself is not patched —
            // gameplay systems (vote count, difficulty, enemy budget, …) must see only
            // the real, in-world players.
            int total = PlayerManager.Instance.GetPlayers().Count + JoinAnytimeManager.PreJoinerCount;
            if (total < 3) { request.Accept(); } else { request.Refuse(); }
            return false;
        }

        private static bool OnPlayerJoinedPrefix(NetworkRunner runner, PlayerRef playerRef) =>
            Disabled || !JoinAnytimeManager.TryHandleOnPlayerJoined(runner, playerRef);

        private static bool OnPlayerLeftPrefix(NetworkRunner runner, PlayerRef playerRef) =>
            Disabled || !JoinAnytimeManager.TryCancelPreJoin(runner, playerRef);

        private static void NextLevelPrefix(GameManager __instance) {
            if (Disabled) { return; }
            if (__instance.Runner == null || !__instance.Runner.IsServer) { return; }
            // Skip the run-finish branch (NextToFinish triggers game-end UI, not a new room).
            if (__instance.LevelProgressionHandler?.NextToFinish == true) { return; }
            JoinAnytimeManager.PromoteAll();
        }

        private static void IntroManagerRpcIntroActivationPostfix() {
            if (Disabled) { return; }
            JoinAnytimeManager.TryPlaceAtSpawnPoint();
        }

        private static void RpcTriggerLevelExitPostfix() {
            if (Disabled) { return; }
            if (!JoinAnytimeManager.HasModdedPreJoiner) { return; }
            ShrineHandler.Instance?.RunStart();
        }

        private static bool DoorManagerRenderPrefix(DoorManager __instance) =>
            _doorPageField?.GetValue(__instance) != null;

        private static bool RpcClassBonusActivatedPrefix(int playerSlotId) {
            if (Disabled) { return true; }
            var player = PlayerManager.Instance?.GetPlayerBySlot(playerSlotId);
            return player == null || !JoinAnytimeManager.IsUnmoddedPromotedJoiner(player.FusionPlayerRef);
        }

        private static bool PerPlayerShrineDataActivatePrefix(object __instance, NetworkRunner runner) {
            if (Disabled) { return true; }
            if (!runner.IsServer) { return true; }
            var slotIndex = (int)_perPlayerDataSlotIndexProp.GetValue(__instance);
            var playerRef = PlayerManager.Instance?.GetPlayerRefBySlot(slotIndex);
            if (playerRef == null || !JoinAnytimeManager.IsUnmoddedPromotedJoiner(playerRef.Value)) { return true; }
            JoinAnytimeManager.SpawnPerkChoices(runner, playerRef.Value, slotIndex);
            return false;
        }

        private static void RpcOnPerkPickupPostfix(RewardManager __instance, NetworkObject networkObj) =>
            JoinAnytimeManager.OnPerkPickupCollected(__instance.Runner, networkObj);

        private static void ChampionAfterSpawnedPostfix(NetworkChampionBase __instance) {
            if (Disabled) { return; }
            JoinAnytimeManager.TryApplyAveraging(__instance);
        }
    }
}
