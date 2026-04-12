using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game;
using RR.Game.Character;
using RR.Game.Enemies;
using RR.Game.Perk;
using RR.Game.Stats;
using RR.Level;
using RR.Utility;
using ThePit.FeralEngine;
using UnityEngine;

namespace ThePit.Patch {
    public static class ThePitPatch {
        internal static FieldInfo HealthStatsField;
        private static MethodInfo HealthLifeStateSetter;
        internal static MethodInfo HealthInjurySetter;
        private static MethodInfo _cooldownTimerSetter;
        private static MethodInfo _cooldownTimerGetter;
        private static MethodInfo _chargeActualSetter;
        private static MethodInfo _chargeCountGetter;
        internal static MethodInfo CombatTimePreciseSetter;
        internal static MethodInfo CombatTimeInSecSetter;
        // True while SetupDoorInformation is executing — prevents NextToFinishPostfix from
        // triggering the loop-door branch inside SetupDoorInformation itself.
        private static bool _inSetupDoorInfo;
        private const string SlashBashScene = "Assets/Scenes/01_Meat_Factory_Scenes/MF_Boss_SlashBash.unity";



        public static bool Apply(Harmony harmony) {
            // --- Critical: perk/XP drip entry point ---
            var beginLevel = AccessTools.Method(typeof(BackendManager), "EventBeginLevel");
            if (beginLevel == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BackendManager.EventBeginLevel not found — mod disabled.");
                return false;
            }
            harmony.Patch(beginLevel,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(EventBeginLevelPostfix))));

            // --- Always use the Slash & Bash arena for MiniBoss rooms in ThePit ---
            var getRandomizeScene = AccessTools.Method(typeof(LevelProgressionHandler), "GetRandomizeScene",
                new[] { typeof(LevelType), typeof(int) });
            if (getRandomizeScene != null) {
                harmony.Patch(getRandomizeScene,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(GetRandomizeScenePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: LevelProgressionHandler.GetRandomizeScene not found — random MiniBoss scene may load.");
            }

            // --- MiniBoss room redirect: override NextStepOptions before DoorManager builds door cards ---
            // SetupDoorInformation writes NextStepOptions into the NetworkArray DoorInfos, which Fusion
            // syncs to all clients. By forcing Type=MiniBoss here the door shows correctly everywhere
            // AND GoToNextLevel naturally calls GetRandomizeScene(MiniBoss,...) with no postfix needed.
            var setupDoorInfo = AccessTools.Method(typeof(DoorManager), "SetupDoorInformation");
            if (setupDoorInfo != null) {
                harmony.Patch(setupDoorInfo,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(SetupDoorInformationPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(SetupDoorInformationPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: DoorManager.SetupDoorInformation not found — door will show wrong type.");
            }

            // --- Suppress DoorManager.Activate mid-match; allow after MatchEnded so winner can use the door.
            var doorActivate = AccessTools.Method(typeof(DoorManager), "Activate");
            if (doorActivate != null) {
                harmony.Patch(doorActivate,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(DoorActivatePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: DoorManager.Activate not found — door may appear in SlashBash room.");
            }


            // --- Health._stats field + LifeState setter — needed by kill tracking / respawn / die-prefix ---
            HealthStatsField = AccessTools.Field(typeof(Health), "_stats");
            if (HealthStatsField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health._stats not found — kill tracking and respawn inactive.");
            }

            HealthLifeStateSetter = AccessTools.PropertySetter(typeof(Health), "LifeState");
            if (HealthLifeStateSetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.LifeState setter not found — KnockedOut skip inactive.");
            }

            HealthInjurySetter = AccessTools.PropertySetter(typeof(Health), "Injury");
            if (HealthInjurySetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.Injury setter not found — injury will not clear on respawn.");
            }

            _cooldownTimerSetter = AccessTools.PropertySetter(typeof(ChampionAbilityWithCooldown), "CooldownTimer");
            if (_cooldownTimerSetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown.CooldownTimer setter not found — ability lock on respawn inactive.");
            }

            _cooldownTimerGetter = AccessTools.PropertyGetter(typeof(ChampionAbilityWithCooldown), "CooldownTimer");
            if (_cooldownTimerGetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown.CooldownTimer getter not found — ult cooldown preservation on respawn inactive.");
            }

            _chargeActualSetter = AccessTools.PropertySetter(typeof(ChampionAbilityWithCooldown), "Charge_Actual");
            if (_chargeActualSetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown.Charge_Actual setter not found — ability lock on respawn inactive.");
            }

            _chargeCountGetter = AccessTools.PropertyGetter(typeof(ChampionAbilityWithCooldown), "ChargeCount");
            if (_chargeCountGetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown.ChargeCount getter not found — full charge restore on grace end inactive.");
            }

            // --- Skip KnockedOut state: champions die immediately, no teammate-revive window ---
            // --- Kill tracking + respawn trigger ---
            var die = AccessTools.Method(typeof(Health), "Die");
            if (die != null) {
                harmony.Patch(die,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(DiePrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(DiePostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.Die not found — kill tracking and respawn inactive.");
            }

            // --- Enemy spawn suppression (dynamic waves) ---
            var spawnActivate = AccessTools.Method(typeof(EnemySpawnManager), "Activate");
            if (spawnActivate != null) {
                harmony.Patch(spawnActivate,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(EnemySpawnActivatePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: EnemySpawnManager.Activate not found — dynamic waves may spawn.");
            }

            // --- Reposition player spawn points around the floor door in the SlashBash arena ---
            var cacheSpawnPoints = AccessTools.Method(typeof(LevelManager), "CacheSpawnPoints");
            if (cacheSpawnPoints != null) {
                harmony.Patch(cacheSpawnPoints,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(CacheSpawnPointsPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: LevelManager.CacheSpawnPoints not found — arena spawn positions unchanged.");
            }

            // --- Suppress arena walk-in cutscene movement and rotation override ---
            // CutsceneMovement() overrides _lookAngle every frame while _cutsceneMovement is true.
            // ThePit has no bosses so the cutscene serves no purpose; block the two methods that
            // move/rotate champions so _cutsceneMovement still disables input but rotation is ours.
            var cutsceneMove = AccessTools.Method(typeof(NetworkChampionBase), "CutsceneMovement");
            if (cutsceneMove != null) {
                harmony.Patch(cutsceneMove,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(CutsceneMovementPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionBase.CutsceneMovement not found — rotation may be overridden during arena entry.");
            }

            var teleportToStart = AccessTools.Method(typeof(NetworkChampionBase), "TeleportToStartWalkPosition");
            if (teleportToStart != null) {
                harmony.Patch(teleportToStart,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(TeleportToStartWalkPositionPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: NetworkChampionBase.TeleportToStartWalkPosition not found — CharMesh may offset during arena entry.");
            }

            // --- Despawn pre-placed enemies (MiniBoss room has Slash & Bash pre-placed in scene) ---
            var sceneInit = AccessTools.Method(typeof(EnemySpawnManager), "SceneInit");
            if (sceneInit != null) {
                harmony.Patch(sceneInit,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(EnemySpawnSceneInitPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: EnemySpawnManager.SceneInit not found — pre-placed enemies will remain.");
            }

            // --- Force Meat Factory biome ---
            var resetProgress = AccessTools.Method(typeof(LevelProgressionHandler), "ResetProgress");
            if (resetProgress != null) {
                harmony.Patch(resetProgress,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(ResetProgressPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: LevelProgressionHandler.ResetProgress not found — biome may vary.");
            }

            // --- Suppress room-clear reward drops in the start room (chests, equipment, etc.) ---
            var rewardActivate = AccessTools.Method(typeof(RewardManager), nameof(RewardManager.Activate));
            if (rewardActivate != null) {
                harmony.Patch(rewardActivate,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(RewardActivatePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: RewardManager.Activate not found — start room chests may appear.");
            }

            // --- Suppress loot container (chest) spawns in both ThePit rooms ---
            var vendorSceneInit = AccessTools.Method(typeof(LevelVendorManager), nameof(LevelVendorManager.SceneInit));
            if (vendorSceneInit != null) {
                harmony.Patch(vendorSceneInit,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(VendorSceneInitPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: LevelVendorManager.SceneInit not found — loot containers may spawn.");
            }

            // --- Block "all players dead" game-over screen so respawn can fire ---
            var outroActivate = AccessTools.Method(typeof(OutroManager), nameof(OutroManager.Activate));
            if (outroActivate != null) {
                harmony.Patch(outroActivate,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(OutroActivatePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: OutroManager.Activate not found — death screen will not be suppressed.");
            }

            // --- Reset ThePit state when host manually returns to lobby mid-match ---
            // ReturnToLobbyCoroutine already resets before calling RPC_Handle_ReturnToLobby,
            // so the normal match-end path is covered. This catches the case where the host
            // returns via the in-game menu before our timer fires.
            var returnToLobby = AccessTools.Method(typeof(GameManager), "RPC_Handle_ReturnToLobby");
            if (returnToLobby != null) {
                harmony.Patch(returnToLobby,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(ReturnToLobbyPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: GameManager.RPC_Handle_ReturnToLobby not found — state may not reset on hub return.");
            }

            // --- Make NextLevel take the stats-page + lobby path on match end ---
            // NextToFinish=true causes NextLevel to call ShowGameEndPage + HandleEvent_GameEnd
            // instead of loading a dungeon scene. LevelType.Lobby has no scene path, so without
            // this the call to SceneRef.FromIndex would throw.
            var nextToFinish = AccessTools.PropertyGetter(typeof(LevelProgressionHandler), "NextToFinish");
            if (nextToFinish != null) {
                harmony.Patch(nextToFinish,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(NextToFinishPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: LevelProgressionHandler.NextToFinish not found — arena exit will crash on lobby door.");
            }

            // --- Level-based damage reduction (Draft-specific) ---
            // Separate from FeralCore's self-damage / invincibility block so variants
            // can opt in or out independently.
            MethodInfo takeBasicDamage = null;
            foreach (var m in typeof(StatsManager).GetMethods()) {
                if (m.Name != "TakeBasicDamage") { continue; }
                var p = m.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType != typeof(float)) { takeBasicDamage = m; break; }
            }
            if (takeBasicDamage != null) {
                harmony.Patch(takeBasicDamage,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(DamageReductionPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: StatsManager.TakeBasicDamage(DamageDescriptor,...) not found — level-based damage reduction inactive.");
            }

            var takeDotDamage = AccessTools.Method(typeof(Health), "TakeDOTDamage");
            if (takeDotDamage != null) {
                harmony.Patch(takeDotDamage,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(DotDamageReductionPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.TakeDOTDamage not found — DoT damage reduction inactive.");
            }

            // --- Heal reduction: mirrors level-based damage reduction ---
            var addHealth = AccessTools.Method(typeof(Health), "AddHealth");
            if (addHealth != null) {
                harmony.Patch(addHealth,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(HealReductionPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.AddHealth not found — heal reduction inactive.");
            }

            // --- Perk filter: exclude PvP-incompatible perks from the selectable pool ---
            // Patch IsItUnlocked (upstream of selection) so banned perks never enter the draw.
            // Patching the output of GetRandomPerkAmount would leave the shrine with fewer than
            // 3 slots filled, causing the UI to freeze waiting for a pick that isn't there.
            var isItUnlocked = AccessTools.Method(typeof(PerkDescriptor), "IsItUnlocked");
            if (isItUnlocked != null) {
                harmony.Patch(isItUnlocked,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(IsItUnlockedPostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: PerkDescriptor.IsItUnlocked not found — perk filter inactive.");
            }

            // --- Lobby planning table: intercept for match config overlay ---
            PlanningTablePatch.Apply(harmony);

            // --- HUD combat timer: drive CombatTimePrecise after the arena grace period ---
            CombatTimePreciseSetter = AccessTools.PropertySetter(typeof(DifficultyManager), "CombatTimePrecise");
            if (CombatTimePreciseSetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: DifficultyManager.CombatTimePrecise setter not found — HUD timer will not start.");
            }
            CombatTimeInSecSetter = AccessTools.PropertySetter(typeof(DifficultyManager), "CombatTimeInSec");
            if (CombatTimeInSecSetter == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: DifficultyManager.CombatTimeInSec setter not found — HUD timer will not start.");
            }
            var dmFixedUpdate = AccessTools.Method(typeof(DifficultyManager), "FixedUpdateNetwork");
            if (dmFixedUpdate != null) {
                harmony.Patch(dmFixedUpdate,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(DifficultyManagerFixedUpdatePostfix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: DifficultyManager.FixedUpdateNetwork not found — HUD timer will not start.");
            }

            ThePitMod.PublicLogger.LogInfo("ThePit: patch applied.");
            return true;
        }

        // ── Ability cooldown lock ────────────────────────────────────────────────

        // Set Power/Special/Defensive/Ultimate into cooldown for `seconds` so the
        // skill bar shows the cooldown timer during the invincibility window.
        // FixedUpdateNetwork auto-restores Charge_Actual when the timer expires —
        // no EnableAllAbilities() needed.
        internal static void UnlockChampionAbilities(NetworkChampionBase champ) {
            if (_cooldownTimerSetter == null) { return; }
            foreach (var ability in new ChampionAbility[] { champ.Power, champ.Special, champ.Defensive, champ.Ultimate }) {
                if (ability is not ChampionAbilityWithCooldown cd) { continue; }
                _cooldownTimerSetter.Invoke(cd, new object[] { default(PausableTickTimer) });
                if (_chargeActualSetter != null && _chargeCountGetter != null) {
                    var maxCharges = (byte)_chargeCountGetter.Invoke(cd, null);
                    _chargeActualSetter.Invoke(cd, new object[] { maxCharges });
                }
            }
        }

        internal static void LockChampionAbilitiesFor(NetworkChampionBase champ, float seconds) {
            if (_cooldownTimerSetter == null || _chargeActualSetter == null) { return; }
            var runner = champ.Runner;
            if (runner == null) { return; }
            var defaultTimer = PausableTickTimer.CreateFromSeconds(runner, seconds);
            foreach (var ability in new ChampionAbility[] { champ.Power, champ.Special, champ.Defensive, champ.Ultimate }) {
                if (ability is not ChampionAbilityWithCooldown cd) { continue; }
                var lockTimer = defaultTimer;
                if (ability == champ.Ultimate && _cooldownTimerGetter != null) {
                    var current = (PausableTickTimer)_cooldownTimerGetter.Invoke(cd, null);
                    if (current.IsRunning) {
                        float remaining = current.RemainingTime(runner);
                        if (remaining > seconds) {
                            lockTimer = PausableTickTimer.CreateFromSeconds(runner, remaining);
                        }
                    }
                }
                _chargeActualSetter.Invoke(cd, new object[] { (byte)0 });
                _cooldownTimerSetter.Invoke(cd, new object[] { lockTimer });
            }
        }

        // ── Initial XP level ─────────────────────────────────────────────────────

        // Sets a champion's XP to the configured starting level immediately after XPLevelReset.
        // At level N: XP.Amount = limits[N-1], AbilityPoints = N-1.
        // At max level (N == limits.Count) all ability upgrades are applied automatically and
        // AbilityPoints is zeroed — nothing left to spend.
        private static void ApplyInitialLevel(NetworkChampionBase champ) {
            int targetLevel = ThePitState.InitialLevelOverride;
            if (targetLevel <= 1) { return; }
            var rdb = RewardDatabase.Instance;
            var limits = rdb?.XPDescriptor?.XPLimits;
            if (limits == null || targetLevel - 1 >= limits.Count) { return; }
            int targetXP = limits[targetLevel - 1];
            champ.XP.Amount = targetXP;
            champ.XP.AbilityPoints = rdb.GetXPUpgradePoints(0, targetXP);

            if (targetLevel < limits.Count) { return; }

            // Max level: auto-spend all ability points. Attack is already at level 1 from
            // XPLevelReset; upgrade remaining levels. Ultimate needs developerUpgrade because
            // it normally requires the champion to be at level 5/10/15/20 per tier.
            champ.Attack.OnUpgraded(champ.Attack.MaximumXPLevel - champ.Attack.ActualXPLevel, developerUpgrade: true);
            champ.Power.OnUpgraded(champ.Power.MaximumXPLevel, developerUpgrade: true);
            champ.Special.OnUpgraded(champ.Special.MaximumXPLevel, developerUpgrade: true);
            champ.Defensive.OnUpgraded(champ.Defensive.MaximumXPLevel, developerUpgrade: true);
            champ.Ultimate.OnUpgraded(champ.Ultimate.MaximumXPLevel, developerUpgrade: true);
            champ.XP.AbilityPoints = 0;
        }

        // ── Entry point ─────────────────────────────────────────────────────────

        private static void EventBeginLevelPostfix() {
            if (!ThePitState.IsDraftMode) {
                return;
            }

            var runner = PlayerManager.Instance?.Runner;
            if (runner == null || !runner.IsServer) {
                return;
            }

            if (!ThePitState.MatchStarted) {
                // First room: init state, drop perks, open door immediately.
                ThePitState.ResetMatchState();
                ThePitState.MatchStarted = true;
                foreach (var player in PlayerManager.Instance.GetPlayers()) {
                    var champ = player.PlayableChampion;
                    if (champ == null) { continue; }
                    champ.XPLevelReset(XPUnlocksEnabled: false);
                    ApplyInitialLevel(champ);
                }

                PerkDripController.StartDrip();
                MatchController.CreateInstance();
            } else if (ThePitState.MiniBossRedirected && !ThePitState.ArenaEntered) {
                // SlashBash room: start grace period + match timer.
                ThePitState.ArenaEntered = true;
                MatchController.StartArena();
            }
        }

        // ── MiniBoss room redirect ───────────────────────────────────────────────

        // Always serve the Slash & Bash arena when the game randomises a MiniBoss scene.
        // Guard on MatchEnded so the arena exit door doesn't loop back here.
        private static bool GetRandomizeScenePrefix(LevelType levelType, ref string __result) {
            if (!ThePitState.IsDraftMode) {
                return true;
            }

            if (ThePitState.MatchEnded) {
                return true;
            }

            if (!ThePitState.MiniBossRedirected) {
                return true;
            }

            FeralCore.Activate();
            __result = SlashBashScene;
            return false;
        }

        // Replace NextStepOptions with a single MiniBoss entry before DoorManager
        // builds DoorInfos. The NetworkArray then syncs the MiniBoss door type to all
        // clients, and GoToNextLevel naturally loads the MiniBoss scene.
        private static void SetupDoorInformationPrefix() {
            // Safety-reset: clear any leftover state from a previous run that may have
            // been interrupted before SetupDoorInformationPostfix fired.
            _inSetupDoorInfo = false;

            if (!ThePitState.IsDraftMode) {
                return;
            }

            // Suppress NextToFinishPostfix while SetupDoorInformation runs so the
            // game's loop-door branch (guarded by NextToFinish) doesn't add extra cards.
            _inSetupDoorInfo = true;

            var lph = GameManager.Instance?.LevelProgressionHandler;
            var opts = lph?.NextStepOptions;
            if (opts == null || opts.Count == 0) {
                return;
            }

            // Arena exit: offer a single Lobby door so the normal vote → animation → lobby path works.
            if (ThePitState.MatchEnded) {
                opts[0].Type = LevelType.Lobby;
                while (opts.Count > 1) {
                    opts.RemoveAt(opts.Count - 1);
                }
                ThePitMod.PublicLogger.LogInfo("ThePit: NextStepOptions overridden to Lobby (match ended).");
                return;
            }

            if (ThePitState.MiniBossRedirected) {
                return;
            }

            opts[0].Type = LevelType.Mystery;
            opts[0].RewardBase = LevelRewardBase.LevelSpecial;
            opts[0].RewardBonus = LevelRewardBase.Experience;
            while (opts.Count > 1) {
                opts.RemoveAt(opts.Count - 1);
            }

            ThePitState.MiniBossRedirected = true;
            ThePitMod.PublicLogger.LogInfo("ThePit: NextStepOptions overridden to Mystery.");
        }

        private static void SetupDoorInformationPostfix() => _inSetupDoorInfo = false;

        // ── Death: skip KnockedOut, go straight to Dead ──────────────────────────

        // Prevents teammates from reviving — our respawn coroutine handles it instead.
        private static bool DiePrefix(Health __instance) {
            if (!ThePitState.IsDraftMode) { return true; }
            if (__instance.Runner?.IsServer != true) { return true; }
            if (HealthStatsField == null || HealthLifeStateSetter == null) { return true; }
            var stats = (StatsManager)HealthStatsField.GetValue(__instance);
            if (stats == null || !stats.IsChampion) { return true; }
            if (!__instance.IsAlive) { return true; }
            HealthLifeStateSetter.Invoke(__instance, new object[] { LifeState.Dead });
            return true; // still run original for events/kill tracking
        }

        // ── Death: kill tracking + respawn ───────────────────────────────────────

        private static void DiePostfix(Health __instance, StatsManager attacker) {
            if (!ThePitState.IsDraftMode) {
                return;
            }

            if (__instance.Runner?.IsServer != true) {
                return;
            }

            if (HealthStatsField == null) {
                return;
            }

            var victimStats = (StatsManager)HealthStatsField.GetValue(__instance);
            if (victimStats == null || !victimStats.IsChampion) {
                return;
            }

            // Track kill for the attacker if they are a player champion.
            if (attacker?.IsChampion == true && !ThePitState.MatchEnded) {
                ThePitState.KillCounts.TryGetValue(attacker.ActorID, out int kills);
                ThePitState.KillCounts[attacker.ActorID] = kills + 1;
            }

            // Trigger respawn unless the match has ended (end-sequence kills should not loop).
            if (!ThePitState.MatchEnded) {
                MatchController.TriggerRespawn(victimStats.ActorID);
            }
        }


        // ── Biome override ───────────────────────────────────────────────────────

        private static void ResetProgressPrefix(LevelProgressionHandler __instance) {
            if (!ThePitState.IsDraftMode) { return; }
            __instance.CurrentBiome = BiomeType.MeatFactory;
        }

        // ── Start room reward suppression ────────────────────────────────────────

        // Skip RewardManager.Activate in the start room — no chests or drops there.
        // In the SlashBash room (ArenaEntered=true) we still skip since we handle
        // the run end ourselves via ReturnToLobby.
        private static bool RewardActivatePrefix() => !ThePitState.IsDraftMode;

        // Skip LevelVendorManager.SceneInit in ThePit rooms — prevents loot containers
        // (regular and locked chests containing coins, souvenirs, equipment) from spawning.
        private static bool VendorSceneInitPrefix() => !ThePitState.IsDraftMode;

        // ── Door handling ────────────────────────────────────────────────────────

        // When the winner steps on the trap door after match end, let the normal door flow run.
        // SetupDoorInformationPrefix ensures DoorInfos[0] = Lobby, so the page shows one
        // "Return to Lobby" option and the normal vote → animation → lobby path handles the rest.
        private static bool DoorPickupCollectedPrefix() {
            if (!ThePitState.IsDraftMode || !ThePitState.MatchEnded) { return true; }
            return true;
        }

        // Allow DoorManager.Activate only when:
        //   • not in draft mode (pass-through)
        //   • in draft mode, start room (MiniBossRedirected=false), chest phase over
        //   • match has ended — spawns the trap door so the winner can step on it
        // Block mid-match in the arena (MiniBossRedirected=true, !MatchEnded).
        private static bool DoorActivatePrefix() {
            if (!ThePitState.IsDraftMode) { return true; }
            if (ThePitState.MatchEnded) { return true; }
            if (ThePitState.ChestPhaseActive) { return false; }
            return !ThePitState.MiniBossRedirected;
        }

        // ── Arena spawn point relocation ─────────────────────────────────────────

        // Reposition the three PlayerSpawnPoint GameObjects around the floor door so
        // the game's own InitPlayerCharacterAtSpawnPoint places champions correctly.
        // Only runs in the SlashBash arena (ArenaEntered becomes true one tick later,
        // so we gate on MiniBossRedirected instead).
        private static void CacheSpawnPointsPostfix() {
            if (!ThePitState.IsDraftMode || !ThePitState.MiniBossRedirected) { return; }

            var doorGo = GameObject.Find("DoorSpawnPoint");
            if (doorGo == null) { return; }

            Vector3 center = doorGo.transform.position;
            const float radius = 7.5f;

            for (int i = 0; i < 3; i++) {
                var sp = GameObject.Find($"PlayerSpawnPoints/PlayerSpawnPoint{i}");
                if (sp == null) { continue; }
                float angle = Mathf.PI * 2f / 3f * i;
                // Position only — leave rotation untouched so the camera rig is unaffected.
                // Player facing is handled in MatchController.ArenaGraceCoroutine (arena entry)
                // and RespawnCoroutine (post-respawn) directly on the champion.
                sp.transform.position = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            }
        }

        // After CutsceneMovement sets _lookAngle = _cutsceneAngle for this champion,
        // override every player's facing toward the door from their own position.
        // Only runs when we have server authority so the write happens once.
        private static bool _cutscenePostfixLogged;

        private static void CutsceneMovementPostfix(NetworkChampionBase __instance) {
            if (!ThePitState.IsActive) { return; }
            if (!_cutscenePostfixLogged) {
                ThePitMod.PublicLogger.LogInfo($"[ThePit] CutsceneMovementPostfix called — IsServer={__instance.Runner?.IsServer}");
                _cutscenePostfixLogged = true;
            }
            if (__instance.Runner?.IsServer != true) { return; }
            var doorGo = GameObject.Find("DoorSpawnPoint");
            if (doorGo == null) {
                ThePitMod.PublicLogger.LogWarning("[ThePit] CutsceneMovementPostfix: DoorSpawnPoint not found");
                return;
            }
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                p.PlayableChampion?.LookToPosition(doorGo.transform.position);
            }
        }

        // Block TeleportToStartWalkPosition so the CharMesh is not offset backward before the walk.
        private static bool TeleportToStartWalkPositionPrefix() => !ThePitState.IsActive;

        // ── Spawn suppression ────────────────────────────────────────────────────

        // Block the wave director from activating (prevents dynamic enemy spawns).
        private static bool EnemySpawnActivatePrefix() => !ThePitState.IsDraftMode;

        // Despawn any pre-placed enemies (Slash & Bash are pre-placed in the MiniBoss scene)
        // and deactivate all traps so the arena is a clean PvP space.
        private static void EnemySpawnSceneInitPostfix(EnemySpawnManager __instance) {
            if (!ThePitState.IsDraftMode) {
                return;
            }

            if (__instance.Runner?.IsServer != true) {
                return;
            }

            for (int i = NetworkEnemyBase.AllEnemies.Count - 1; i >= 0; i--) {
                var enemy = NetworkEnemyBase.AllEnemies[i];
                if (enemy?.Object != null) {
                    __instance.Runner.Despawn(enemy.Object);
                }
            }

            foreach (var trap in TrapBase.AllTraps) {
                trap?.TurnOffTrap();
            }
        }

        // ── Death screen suppression ─────────────────────────────────────────────

        // Block the "all players dead" game-over sequence while IsDraftMode is true —
        // both during the match (so respawn can fire) and after MatchEnded (so the
        // death cutscene doesn't fire when we kill losers, bypassing our 20s delay).
        // ReturnToLobbyCoroutine sets IsActive=false before calling RPC_Handle_ReturnToLobby,
        // which makes IsDraftMode false and lets the prefix become a no-op.
        // Block the "all players dead" outro only while the match is still running.
        // Once MatchEnded=true (EndMatch fired) let it through — or after ResetMatchState
        // on hub return the flag is already clear.
        private static bool OutroActivatePrefix(OutroManager.OutroReason reason) {
            if (!ThePitState.IsDraftMode) { return true; }
            if (reason != OutroManager.OutroReason.AllPlayerDead) { return true; }
            return ThePitState.MatchEnded;
        }

        // ── Arena exit: return to lobby instead of loading a dungeon scene ──────

        // When MatchEnded, make NextLevel believe the run is finished so it takes the
        // ShowGameEndPage + HandleEvent_GameEnd path instead of loading a dungeon scene.
        private static void NextToFinishPostfix(ref bool __result) {
            if (ThePitState.MatchEnded && !_inSetupDoorInfo) { __result = true; }
        }

        // ── Hub return: reset ThePit state on manual exit ────────────────────────

        // Catches both the normal match-end path (via GameEndScreenExit after the stats page)
        // and the mid-match manual hub return.
        private static void ReturnToLobbyPostfix() {
            if (!ThePitState.IsDraftMode) { return; }
            _cutscenePostfixLogged = false;
            foreach (var p in PlayerManager.Instance.GetPlayers()) {
                p.PlayableChampion?.Stats?.ClearActualEffects();
            }
            ThePitState.ResetMatchState();
            MatchController.Stop();
        }

        // ── Level-based damage reduction ─────────────────────────────────────────

        // Scales champion-on-champion damage down based on the victim's XP level.
        // At level 1: full damage. At max XP level with maxFactor=20: 1/20th damage.
        // Formula: divisor = Lerp(1, maxFactor, (level-1) / (maxLevel-1))
        private static bool DamageReductionPrefix(StatsManager __instance, ref DamageDescriptor dmgDesc, StatsManager attacker) {
            if (!ThePitState.IsDraftMode) { return true; }
            if (attacker == null || !attacker.IsChampion || !__instance.IsChampion) { return true; }

            float maxFactor = ThePitState.CachedDamageReductionFactor;
            if (maxFactor <= 1f) { return true; }

            var rdb = RewardDatabase.Instance;
            if (rdb == null || __instance.Champion == null) { return true; }

            const int MaxXpLevel = 20;
            int level = rdb.GetXPLevel(__instance.Champion.XP.Amount);
            if (level <= 1) { return true; }

            float t = (float)(level - 1) / (MaxXpLevel - 1);
            float divisor = Mathf.Lerp(1f, maxFactor, t);
            dmgDesc = dmgDesc.CloneAndMultiply(1f / divisor);
            return true;
        }

        private static void DotDamageReductionPrefix(Health __instance, ref float damage, StatsManager attacker) {
            if (!ThePitState.IsDraftMode) { return; }
            if (attacker == null || !attacker.IsChampion) { return; }
            if (HealthStatsField == null) { return; }

            var victimStats = HealthStatsField.GetValue(__instance) as StatsManager;
            if (victimStats == null || !victimStats.IsChampion) { return; }

            float maxFactor = ThePitState.CachedDamageReductionFactor;
            if (maxFactor <= 1f) { return; }

            var rdb = RewardDatabase.Instance;
            if (rdb == null || victimStats.Champion == null) { return; }

            const int MaxXpLevel = 20;
            int level = rdb.GetXPLevel(victimStats.Champion.XP.Amount);
            if (level <= 1) { return; }

            float t = (float)(level - 1) / (MaxXpLevel - 1);
            float divisor = Mathf.Lerp(1f, maxFactor, t);
            damage /= divisor;
        }

        // ── Heal reduction ───────────────────────────────────────────────────────

        // Mirrors level-based damage reduction so that healing scales proportionally
        // to incoming damage — prevents high-level players from out-healing reduced damage.
        private static void HealReductionPrefix(Health __instance, ref float value) {
            if (!ThePitState.IsDraftMode) { return; }
            if (HealthStatsField == null) { return; }
            var stats = HealthStatsField.GetValue(__instance) as StatsManager;
            if (stats == null || !stats.IsChampion || stats.Champion == null) { return; }
            float maxFactor = ThePitState.CachedDamageReductionFactor;
            if (maxFactor <= 1f) { return; }
            var rdb = RewardDatabase.Instance;
            if (rdb == null) { return; }
            const int MaxXpLevel = 20;
            int level = rdb.GetXPLevel(stats.Champion.XP.Amount);
            if (level <= 1) { return; }
            float t = (float)(level - 1) / (MaxXpLevel - 1);
            value /= Mathf.Lerp(1f, maxFactor, t);
        }

        // ── Perk filter ──────────────────────────────────────────────────────────

        private static void IsItUnlockedPostfix(PerkDescriptor __instance, ref bool __result) =>
            ThePitPerkFilter.FilterUnlocked(__instance, ref __result);

        // ── HUD combat timer ─────────────────────────────────────────────────────

        private static void DifficultyManagerFixedUpdatePostfix(DifficultyManager __instance) =>
            MatchController.OnDifficultyFixedUpdate(__instance);
    }

}
