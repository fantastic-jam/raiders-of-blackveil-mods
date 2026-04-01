using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PerfectDodge.Localization;
using RR.Game;
using RR.Game.Character;
using RR.Game.Damage;
using RR.Game.Stats;
using UnityEngine;

namespace PerfectDodge.Patch
{
    public static class PerfectDodgePatch
    {
        // actorID -> absolute Time.time when the perfect-dodge window expires
        private static readonly Dictionary<int, float> _dodgeWindowEndTime = new Dictionary<int, float>();

        // actorIDs waiting for a dash charge refund once the dash animation finishes
        private static readonly HashSet<int> _pendingRefunds = new HashSet<int>();

        // actorIDs that have already fired VFX/SFX feedback for a perfect dodge (to prevent duplicates on multiple hits)
        private static readonly HashSet<int> _perfectDodgeFeedbackFiredThisDash = new HashSet<int>();

        // Cached reflection handles (resolved once in Apply)
        private static FieldInfo _resetAllCooldownField;
        private static FieldInfo _healthStatsField;
        private static FieldInfo _healthStatsUIField;

        public static void Apply(Harmony harmony)
        {
            _resetAllCooldownField = AccessTools.Field(typeof(ChampionAbilityWithCooldown), "_resetAllCooldown");
            if (_resetAllCooldownField == null)
                PerfectDodgeMod.PublicLogger.LogWarning("PerfectDodge: Could not find ChampionAbilityWithCooldown._resetAllCooldown — charge refund disabled.");

            _healthStatsField = AccessTools.Field(typeof(Health), "_stats");
            _healthStatsUIField = AccessTools.Field(typeof(Health), "_statsUI");
            if (_healthStatsField == null)
            {
                PerfectDodgeMod.PublicLogger.LogWarning("PerfectDodge: Could not find Health._stats — patch inactive.");
                return;
            }

            var mainStateChangedMethod = AccessTools.Method(typeof(DashAbility), "MainStateChanged");
            if (mainStateChangedMethod == null)
            {
                PerfectDodgeMod.PublicLogger.LogWarning("PerfectDodge: Could not find DashAbility.MainStateChanged — patch inactive.");
                return;
            }
            harmony.Patch(mainStateChangedMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PerfectDodgePatch), nameof(MainStateChangedPostfix))));

            var takeBasicDamageMethod = AccessTools.Method(typeof(Health), "TakeBasicDamage");
            if (takeBasicDamageMethod == null)
            {
                PerfectDodgeMod.PublicLogger.LogWarning("PerfectDodge: Could not find Health.TakeBasicDamage — patch inactive.");
                return;
            }
            harmony.Patch(takeBasicDamageMethod,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(PerfectDodgePatch), nameof(TakeBasicDamagePrefix))));

            PerfectDodgeMod.PublicLogger.LogInfo("PerfectDodge patches applied.");
        }

        /// <summary>
        /// Postfix on DashAbility.MainStateChanged.
        /// Opens a fixed 0.2 second dodge window when the dash button is pressed (Idle → WindUp/Cast).
        /// Fires VFX/SFX and refunds the charge when the dash action ends (InAction → any), if a perfect dodge was achieved.
        /// </summary>
        public static void MainStateChangedPostfix(DashAbility __instance, ChampionAbility.MainStateValues prevState)
        {
            var stats = __instance.Stats;
            if (stats == null || !stats.IsChampion)
                return;

            int actorId = stats.ActorID;
            var current = __instance.MainState;

            if (prevState == ChampionAbility.MainStateValues.Idle
                && current == ChampionAbility.MainStateValues.Cast)
            {
                // Open a short timing window from dash press, independent of dash windup duration.
                _dodgeWindowEndTime[actorId] = Time.time + PerfectDodgeMod.PerfectDodgeWindowSeconds.Value;
                _perfectDodgeFeedbackFiredThisDash.Remove(actorId);
            }
            else if (prevState == ChampionAbility.MainStateValues.InAction)
            {
                // Dash movement ended — if a perfect dodge happened, refund the charge.
                if (_pendingRefunds.Remove(actorId) && _resetAllCooldownField != null)
                    _resetAllCooldownField.SetValue(__instance, true);
            }
        }

        /// <summary>
        /// Prefix on Health.TakeBasicDamage.
        /// If the target champion is inside an active perfect-dodge window, the hit is intercepted:
        ///   - Damage is fully blocked.
        ///   - The game's OnDodge event fires (reuses existing SFX/VFX).
        ///   - "*dodged*" label and VFX/SFX fire immediately (once per dash, even if multiple hits occur).
        ///   - The dash charge is queued for refund.
        /// </summary>
        public static bool TakeBasicDamagePrefix(
            Health __instance,
            ref DamageDescriptor dmgDesc,
            StatsManager attacker,
            ref bool __result)
        {
            var stats = _healthStatsField.GetValue(__instance) as StatsManager;
            if (stats == null || !stats.IsChampion)
                return true;

            int actorId = stats.ActorID;
            if (!_dodgeWindowEndTime.TryGetValue(actorId, out float endTime) || Time.time > endTime)
                return true;

            // Consume the window; one perfect dodge per dash press
            _dodgeWindowEndTime.Remove(actorId);
            _pendingRefunds.Add(actorId);

            // Fire the game's own OnDodge event (tied to existing SFX/VFX in perks/animations)
            stats.Events.TriggerEvent(CharacterEvent.OnDodge, new TriggerParams(attacker));

            // Fire "*dodged*" feedback once per dash (even if multiple hits occur during the window)
            if (_perfectDodgeFeedbackFiredThisDash.Add(actorId))
            {
                var statsUI = _healthStatsUIField?.GetValue(__instance) as OverheadStatsUI;
                statsUI?.SetStaticText(PerfectDodgeLocalization.Get(PerfectDodgeLocalization.DodgedLabelKey), Color.cyan, 1.5f);
            }

            // Block the hit entirely — skip TakeBasicDamage
            __result = false;
            return false;
        }
    }
}
