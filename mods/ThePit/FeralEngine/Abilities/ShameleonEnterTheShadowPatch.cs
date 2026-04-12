using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Proxy pattern: in PvP draft mode ShameleonEnterTheShadowAbility is a thin proxy.
    // FixedUpdateNetwork and OnCharacterEvent are prefixed and return false, handing all
    // logic to PvpShameleonEnterTheShadow. Base-class FixedUpdateNetwork is invoked
    // explicitly so cooldown management still runs.
    internal static class ShameleonEnterTheShadowPatch {
        private static readonly ConditionalWeakTable<ShameleonEnterTheShadowAbility, PvpShameleonEnterTheShadow> _proxies = new();

        private static MethodInfo _baseFixedUpdate;
        private static MethodInfo _baseOnCharEvent;

        internal static void Apply(Harmony harmony) {
            PvpShameleonEnterTheShadow.Init();

            _baseFixedUpdate = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "FixedUpdateNetwork");
            if (_baseFixedUpdate == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown.FixedUpdateNetwork not found — Shameleon cooldown inactive in PvP.");
            }

            _baseOnCharEvent = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "OnCharacterEvent",
                new[] { typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) })
                ?? AccessTools.Method(typeof(ChampionAbility), "OnCharacterEvent",
                    new[] { typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) });
            if (_baseOnCharEvent == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbility.OnCharacterEvent not found — hit-count tracking inactive in PvP.");
            }

            var spawned = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "Spawned");
            if (spawned != null) {
                harmony.Patch(spawned,
                    postfix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(SpawnedPostfix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.Spawned not found — proxy inactive.");
            }

            var fixedUpdate = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "FixedUpdateNetwork");
            if (fixedUpdate != null) {
                harmony.Patch(fixedUpdate,
                    prefix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(FixedUpdateNetworkPrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.FixedUpdateNetwork not found — proxy inactive.");
            }

            var onCharEvent = AccessTools.Method(typeof(ShameleonEnterTheShadowAbility), "OnCharacterEvent",
                new[] { typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) });
            if (onCharEvent != null) {
                harmony.Patch(onCharEvent,
                    prefix: new HarmonyMethod(typeof(ShameleonEnterTheShadowPatch), nameof(OnCharacterEventPrefix)));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonEnterTheShadowAbility.OnCharacterEvent not found — proxy inactive.");
            }
        }

        private static void SpawnedPostfix(ShameleonEnterTheShadowAbility __instance) {
            _proxies.Remove(__instance);
            _proxies.Add(__instance, new PvpShameleonEnterTheShadow(__instance));
        }

        private static bool FixedUpdateNetworkPrefix(ShameleonEnterTheShadowAbility __instance) {
            if (!ThePitState.IsDraftMode) { return true; }
            _baseFixedUpdate?.Invoke(__instance, null);
            if (_proxies.TryGetValue(__instance, out var s)) { s.OnFixedUpdate(); }
            return false;
        }

        private static bool OnCharacterEventPrefix(ShameleonEnterTheShadowAbility __instance, StatsManager owner, CharacterEvent gameplayEvent, TriggerParams triggerParam) {
            if (!ThePitState.IsDraftMode) { return true; }
            _baseOnCharEvent?.Invoke(__instance, new object[] { owner, gameplayEvent, triggerParam });
            if (_proxies.TryGetValue(__instance, out var s)) { s.OnCharacterEvent(gameplayEvent); }
            return false;
        }

        internal static void Reset() {
            foreach (var a in Object.FindObjectsOfType<ShameleonEnterTheShadowAbility>()) {
                if (_proxies.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }
    }
}
