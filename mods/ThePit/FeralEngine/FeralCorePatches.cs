using System.Reflection;
using HarmonyLib;
using RR.Game;
using RR.Game.Character;
using RR.Game.Perk;
using RR.Game.Stats;
using ThePit.FeralEngine.Abilities;

namespace ThePit.FeralEngine {
    internal static class FeralCorePatches {
        private static FieldInfo _healthStatsField;

        private static MethodInfo _triggerPerkFuncMethod;

        internal static void Apply(Harmony harmony) {
            // ── Reflection setup ──────────────────────────────────────────────────
            _healthStatsField = AccessTools.Field(typeof(Health), "_stats");
            if (_healthStatsField == null) {
                ThePitMod.PublicLogger.LogWarning("FeralCore: Health._stats not found — cross-champion heal block inactive.");
            }

            // ── Damage filtering + level-based reduction ─────────────────────────
            MethodInfo takeBasicDamage = null;
            foreach (var m in typeof(StatsManager).GetMethods()) {
                if (m.Name != "TakeBasicDamage") { continue; }
                var p = m.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType != typeof(float)) { takeBasicDamage = m; break; }
            }
            if (takeBasicDamage != null) {
                harmony.Patch(takeBasicDamage,
                    prefix: new HarmonyMethod(typeof(FeralCorePatches), nameof(TakeBasicDamagePrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("FeralCore: StatsManager.TakeBasicDamage(DamageDescriptor,...) not found — self-damage not blocked.");
            }

            // ── Cross-champion heal prevention ────────────────────────────────────
            var addHealth = AccessTools.Method(typeof(Health), "AddHealth");
            if (addHealth != null) {
                harmony.Patch(addHealth,
                    prefix: new HarmonyMethod(typeof(FeralCorePatches), nameof(AddHealthPrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("FeralCore: Health.AddHealth not found — cross-champion heals not blocked.");
            }

            // ── Ability block while respawn-invincible ────────────────────────────
            var canActivateGetter = AccessTools.PropertyGetter(typeof(ChampionAbility), "CanActivate");
            if (canActivateGetter != null) {
                harmony.Patch(canActivateGetter,
                    prefix: new HarmonyMethod(typeof(FeralCorePatches), nameof(CanActivatePrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("FeralCore: ChampionAbility.CanActivate not found — invincible ability block inactive.");
            }

            // ── Passive perk suppression during grace / death ─────────────────────
            _triggerPerkFuncMethod = AccessTools.Method(typeof(PerkHandler), "TriggerPerkFunc");
            if (_triggerPerkFuncMethod != null) {
                harmony.Patch(_triggerPerkFuncMethod,
                    prefix: new HarmonyMethod(typeof(FeralCorePatches), nameof(TriggerPerkFuncPrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("FeralCore: PerkHandler.TriggerPerkFunc not found — passive perk guard inactive.");
            }

            // ── Ability coverage ──────────────────────────────────────────────────
            AbilityPatch.Apply(harmony);

            ThePitMod.PublicLogger.LogInfo("FeralCore: patches applied.");
        }

        // ── Self-damage prevention + invincibility blocking ───────────────────────
        // Guards stripped — patch only exists while FeralCore is active.
        // Damage reduction is variant-specific and handled separately by ThePit.
        private static bool TakeBasicDamagePrefix(StatsManager __instance, ref DamageDescriptor dmgDesc, StatsManager attacker) {
            if (attacker == null || !attacker.IsChampion || !__instance.IsChampion) { return true; }
            if (attacker.ActorID == __instance.ActorID) { return false; }
            if (FeralCore.IsRespawnInvincible(__instance.ActorID)) { return false; }
            if (FeralCore.IsRespawnInvincible(attacker.ActorID)) { return false; }
            return true;
        }

        // ── Cross-champion heal prevention ────────────────────────────────────────
        private static bool AddHealthPrefix(Health __instance, StatsManager healer) {
            if (healer == null || !healer.IsChampion) { return true; }
            if (_healthStatsField == null) { return true; }
            var targetStats = (StatsManager)_healthStatsField.GetValue(__instance);
            if (targetStats == null || !targetStats.IsChampion) { return true; }
            return healer.ActorID == targetStats.ActorID;
        }

        // ── Ability block while respawn-invincible ────────────────────────────────
        private static bool CanActivatePrefix(ChampionAbility __instance, ref bool __result) {
            var stats = __instance.Stats;
            if (stats == null || !stats.IsChampion) { return true; }
            if (!FeralCore.IsRespawnInvincible(stats.ActorID)) { return true; }
            __result = false;
            return false;
        }

        // ── Passive perk suppression during grace / death ─────────────────────────
        // Blocks all perk function execution (Thunder, Flame Daggers, health gen, etc.)
        // when the perk owner is dead, respawn-invincible, or in a grace-period window.
        private static bool TriggerPerkFuncPrefix(PerkHandler __instance) {
            var stats = __instance.Stats;
            if (stats == null || !stats.IsChampion) { return true; }
            if (!stats.IsAlive) { return false; }
            if (FeralCore.IsRespawnInvincible(stats.ActorID)) { return false; }
            if (stats.Health != null && stats.Health.AllDamageDisabled) { return false; }
            return true;
        }
    }
}
