using System.Reflection;
using HarmonyLib;
using RR;
using RR.Game.Enemies;
using RR.Game.Stats;
using RR.Level;
using ThePit.Patch.Abilities;

namespace ThePit.Patch {
    public static class ThePitPatch {
        internal static FieldInfo HealthStatsField;
        private static MethodInfo HealthLifeStateSetter;
        internal static MethodInfo HealthInjurySetter;

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

            // --- PvP: per-ability champion detection via PvpDetector ---
            ShameleonAttackPatch.Apply(harmony);
            ShameleonShadowStrikePatch.Apply(harmony);
            ShameleonShadowDancePatch.Apply(harmony);
            ShameleonTongueLeapPatch.Apply(harmony);
            BlazeBlastWavePatch.Apply(harmony);
            BlazeSpecialAreaPatch.Apply(harmony);
            SunStrikeAreaPatch.Apply(harmony);
            BeatriceSpecialObjectPatch.Apply(harmony);
            ManEaterPlantBrainPatch.Apply(harmony);
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

            ThePitMod.PublicLogger.LogInfo("ThePit: patch applied.");
            return true;
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

        // ── Door suppression in SlashBash room ──────────────────────────────────

        // Allow DoorManager.Activate in the start room (MiniBossRedirected=false) so the
        // grace-period call opens the MiniBoss door. Block it once we're in SlashBash so
        // no door UI ever appears there — the match ends via ReturnToLobby directly.
        private static bool DoorActivatePrefix() {
            if (!ThePitState.IsDraftMode) {
                return true;
            }

            return !ThePitState.MiniBossRedirected;
        }

        // ── Spawn suppression ────────────────────────────────────────────────────

        // Block the wave director from activating (prevents dynamic enemy spawns).
        private static bool EnemySpawnActivatePrefix() => !ThePitState.IsDraftMode;

        // Despawn any pre-placed enemies (Slash & Bash are pre-placed in the MiniBoss scene).
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
        }

        // ── Death screen suppression ─────────────────────────────────────────────

        // Block the "all players dead" game-over sequence while the match is ongoing
        // so the respawn coroutine can resurrect the player without the run ending.
        private static bool OutroActivatePrefix(OutroManager.OutroReason reason) {
            if (!ThePitState.IsDraftMode) {
                return true;
            }

            if (ThePitState.MatchEnded) {
                return true;
            }

            return reason != OutroManager.OutroReason.AllPlayerDead;
        }

        // ── Self-damage prevention ───────────────────────────────────────────────

        // Champion abilities that detect other champions (Player layer) could hit the
        // caster themselves. Block any damage where attacker and victim are the same champion.
        private static bool TakeBasicDamagePrefix(StatsManager __instance, StatsManager attacker) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return true; }
            if (attacker == null || !attacker.IsChampion || !__instance.IsChampion) { return true; }
            return attacker.ActorID != __instance.ActorID;
        }

        // ── Cross-champion heal prevention ───────────────────────────────────────

        // In co-op, champion heals and heal-area abilities affect all players. In ThePit
        // PvP mode only the caster should receive their own heals — never an opponent.
        private static bool AddHealthPrefix(Health __instance, StatsManager healer) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return true; }
            if (healer == null || !healer.IsChampion) { return true; }
            if (HealthStatsField == null) { return true; }
            var targetStats = (StatsManager)HealthStatsField.GetValue(__instance);
            if (targetStats == null || !targetStats.IsChampion) { return true; }
            return healer.ActorID == targetStats.ActorID;
        }
    }

}
