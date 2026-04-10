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
using ThePit.Patch.Abilities;
using UnityEngine;

namespace ThePit.Patch {
    public static class ThePitPatch {
        internal static FieldInfo HealthStatsField;
        private static MethodInfo HealthLifeStateSetter;
        internal static MethodInfo HealthInjurySetter;
        private static MethodInfo _cooldownTimerSetter;
        private static MethodInfo _cooldownTimerGetter;
        private static MethodInfo _chargeActualSetter;
        internal static MethodInfo CombatTimePreciseSetter;
        internal static MethodInfo CombatTimeInSecSetter;

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
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(SetupDoorInformationPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: DoorManager.SetupDoorInformation not found — door will show wrong type.");
            }

            // --- Suppress DoorManager.Activate in the SlashBash room (MiniBossRedirected=true).
            // In the start room it is still allowed so PerkCoroutine can open the MiniBoss door.
            // This prevents any door UI from appearing in the arena after the match ends.
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

            // --- PvP: per-ability champion detection via PvpDetector ---
            ShameleonAttackPatch.Apply(harmony);
            ShameleonShadowStrikePatch.Apply(harmony);
            ShameleonShadowDancePatch.Apply(harmony);
            ShameleonTongueLeapPatch.Apply(harmony);
            BlazeAttackPatch.Apply(harmony);
            BlazeBlastWavePatch.Apply(harmony);
            BlazeSpecialAreaPatch.Apply(harmony);
            SunStrikeAreaPatch.Apply(harmony);
            BeatriceAttackPatch.Apply(harmony);
            BeatriceEntanglingRootsPatch.Apply(harmony);
            BeatriceLotusFlowerPatch.Apply(harmony);
            BeatriceSpecialObjectPatch.Apply(harmony);
            ManEaterPlantBrainPatch.Apply(harmony);
            WitheredSeedBrainPatch.Apply(harmony);
            ChampionMinionPatch.Apply(harmony);
            AreaCharacterSelectorPatch.Apply(harmony);
            RhinoAttackPatch.Apply(harmony);
            RhinoEarthquakePatch.Apply(harmony);
            RhinoShieldsUpPatch.Apply(harmony);
            RhinoStampedePatch.Apply(harmony);
            RhinoSpinPatch.Apply(harmony);

            // --- Block "all players dead" game-over screen so respawn can fire ---
            var outroActivate = AccessTools.Method(typeof(OutroManager), nameof(OutroManager.Activate));
            if (outroActivate != null) {
                harmony.Patch(outroActivate,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(OutroActivatePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: OutroManager.Activate not found — death screen will not be suppressed.");
            }

            // --- Block self-damage: champion abilities with Player layer added can hit the caster ---
            MethodInfo takeBasicDamage = null;
            foreach (var m in typeof(StatsManager).GetMethods()) {
                if (m.Name != "TakeBasicDamage") { continue; }
                var p = m.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType != typeof(float)) { takeBasicDamage = m; break; }
            }
            if (takeBasicDamage != null) {
                harmony.Patch(takeBasicDamage,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(TakeBasicDamagePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: StatsManager.TakeBasicDamage(DamageDescriptor,...) not found — self-damage not blocked.");
            }

            // --- Block cross-champion healing: ally abilities (heal, barrier area) must not buff enemies ---
            var addHealth = AccessTools.Method(typeof(Health), "AddHealth");
            if (addHealth != null) {
                harmony.Patch(addHealth,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(AddHealthPrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: Health.AddHealth not found — cross-champion heals not blocked.");
            }

            // --- Block ability use while respawn-invincible: preserves cooldowns and prevents
            //     a freshly respawned champion from immediately dealing damage ---
            var canActivateGetter = AccessTools.PropertyGetter(typeof(ChampionAbility), "CanActivate");
            if (canActivateGetter != null) {
                harmony.Patch(canActivateGetter,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(ThePitPatch), nameof(CanActivatePrefix))));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbility.CanActivate not found — invincible ability block inactive.");
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
                    player.PlayableChampion?.XPLevelReset(XPUnlocksEnabled: false);
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
        private static bool GetRandomizeScenePrefix(LevelType levelType, ref string __result) {
            if (!ThePitState.IsDraftMode) {
                return true;
            }

            if (!ThePitState.MiniBossRedirected) {
                return true;
            }

            __result = SlashBashScene;
            return false;
        }

        // Replace NextStepOptions with a single MiniBoss entry before DoorManager
        // builds DoorInfos. The NetworkArray then syncs the MiniBoss door type to all
        // clients, and GoToNextLevel naturally loads the MiniBoss scene.
        private static void SetupDoorInformationPrefix() {
            if (!ThePitState.IsDraftMode) {
                return;
            }

            if (ThePitState.MiniBossRedirected) {
                return;
            }

            var lph = GameManager.Instance?.LevelProgressionHandler;
            var opts = lph?.NextStepOptions;
            if (opts == null || opts.Count == 0) {
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

        // ── Door suppression in SlashBash room ──────────────────────────────────

        // Allow DoorManager.Activate only when:
        //   • not in draft mode (pass-through)
        //   • match ended — door activates in the arena as the visual match-over signal
        //   • in draft mode, start room (MiniBossRedirected=false), and chest phase is over
        // Block during ChestPhaseActive so the game can't open the door between rounds.
        // Block in the arena mid-match (MiniBossRedirected=true, !MatchEnded) so no door
        // UI appears there while fighting.
        private static bool DoorActivatePrefix() {
            if (!ThePitState.IsDraftMode) {
                return true;
            }

            if (ThePitState.MatchEnded) {
                return true;
            }

            if (ThePitState.ChestPhaseActive) {
                return false;
            }

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
            const float radius = 2.5f;

            for (int i = 0; i < 3; i++) {
                var sp = GameObject.Find($"PlayerSpawnPoints/PlayerSpawnPoint{i}");
                if (sp == null) { continue; }
                float angle = Mathf.PI * 2f / 3f * i;
                // Position only — leave rotation untouched so the camera rig is unaffected.
                // Player facing is handled in TeleportAllToCenter (arena entry) and
                // RespawnCoroutine (post-respawn) directly on the champion.
                sp.transform.position = center + new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
            }
        }

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

        // ── Self-damage prevention ───────────────────────────────────────────────

        // Block champion-on-champion damage that our PvP patches introduce:
        //   • Self-damage (caster hits own collider via expanded Player layer)
        //   • Damage to a respawn-invincible victim (bypasses game's IsImmuneOrInvincible
        //     because our invincibility is tracked outside the game engine)
        //   • Damage from a respawn-invincible attacker (in-flight projectiles fired
        //     before CanActivate was blocked still reach targets)
        // Also applies level-based damage reduction so late-game champions are tankier.
        // The overlay/cfg maxFactor controls how much damage is divided at max XP level.
        // Formula: at level L, divisor = Lerp(1, maxFactor, (L-1)/(maxLevel-1))
        //   → level 1: full damage; level 20 with maxFactor=20: 1/20th damage.
        private static bool TakeBasicDamagePrefix(StatsManager __instance, ref DamageDescriptor dmgDesc, StatsManager attacker) {
            if (!ThePitState.IsAttackPossible) { return true; }
            if (attacker == null || !attacker.IsChampion || !__instance.IsChampion) { return true; }
            if (attacker.ActorID == __instance.ActorID) { return false; }
            if (ThePitState.IsPlayerInvincible(__instance.ActorID)) { return false; }
            if (ThePitState.IsPlayerInvincible(attacker.ActorID)) { return false; }

            float maxFactor = ThePitState.DamageReductionMaxFactor > 0f
                ? ThePitState.DamageReductionMaxFactor
                : (ThePitMod.CfgDamageReductionOptions == null ? 20f
                    : ParseDefaultDamageReductionFactor());

            if (maxFactor > 1f) {
                var rdb = RewardDatabase.Instance;
                if (rdb != null && __instance.Champion != null) {
                    const int MaxXpLevel = 20;
                    int level = rdb.GetXPLevel(__instance.Champion.XP.Amount);
                    if (level > 1) {
                        float t = (float)(level - 1) / (MaxXpLevel - 1);
                        float divisor = Mathf.Lerp(1f, maxFactor, t);
                        dmgDesc = dmgDesc.CloneAndMultiply(1f / divisor);
                    }
                }
            }

            return true;
        }

        // Reads the default damage reduction factor from the first entry of the cfg option list.
        // Falls back to 20 (= "Strong") if absent or unparseable.
        // This is only used when no overlay session has run yet (first match after restart).
        private static float ParseDefaultDamageReductionFactor() {
            var raw = ThePitMod.CfgDamageReductionOptions?.Value;
            if (string.IsNullOrEmpty(raw)) { return 20f; }
            try {
                // Default is "Strong:20" at index 3 of the default list — read index 3 if it exists.
                var entries = raw.Split(',');
                int idx = System.Math.Min(3, entries.Length - 1);
                int colon = entries[idx].IndexOf(':');
                if (colon < 0) { return 20f; }
                return float.Parse(entries[idx][(colon + 1)..].Trim(),
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return 20f; }
        }

        // ── Ability block while respawn-invincible ───────────────────────────────

        // Prevent a freshly respawned champion from activating abilities during their
        // invincibility window. Prefix returns false to skip the original and sets
        // __result = false so no cooldown is consumed.
        private static bool CanActivatePrefix(ChampionAbility __instance, ref bool __result) {
            if (!ThePitState.IsAttackPossible) { return true; }
            var stats = __instance.Stats;
            if (stats == null || !stats.IsChampion) { return true; }
            if (!ThePitState.IsPlayerInvincible(stats.ActorID)) { return true; }
            __result = false;
            return false;
        }

        // ── Hub return: reset ThePit state on manual exit ────────────────────────

        // Catches the path where the host presses "Return to Hub" before the match
        // timer fires (bypassing ReturnToLobbyCoroutine). When the coroutine runs
        // normally it sets IsActive = false first, so IsDraftMode is already false
        // by the time this fires — making it a no-op in that case.
        private static void ReturnToLobbyPostfix() {
            if (!ThePitState.IsDraftMode) { return; }
            ThePitState.ResetMatchState();
            MatchController.Stop();
        }

        // ── Cross-champion heal prevention ───────────────────────────────────────

        // In co-op, champion heals and heal-area abilities affect all players. In ThePit
        // PvP mode only the caster should receive their own heals — never an opponent.
        private static bool AddHealthPrefix(Health __instance, StatsManager healer) {
            if (!ThePitState.IsAttackPossible) { return true; }
            if (healer == null || !healer.IsChampion) { return true; }
            if (HealthStatsField == null) { return true; }
            var targetStats = (StatsManager)HealthStatsField.GetValue(__instance);
            if (targetStats == null || !targetStats.IsChampion) { return true; }
            return healer.ActorID == targetStats.ActorID;
        }

        // ── Perk filter ──────────────────────────────────────────────────────────

        private static void IsItUnlockedPostfix(PerkDescriptor __instance, ref bool __result) =>
            ThePitPerkFilter.FilterUnlocked(__instance, ref __result);

        // ── HUD combat timer ─────────────────────────────────────────────────────

        private static void DifficultyManagerFixedUpdatePostfix(DifficultyManager __instance) =>
            MatchController.OnDifficultyFixedUpdate(__instance);
    }

}
