using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RR.Game;
using RR.Game.Character;
using RR.Game.Stats;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Proxy pattern: in PvP draft mode ShameleonEnterTheShadowAbility is a thin proxy.
    // FixedUpdateNetwork and OnCharacterEvent are prefixed and return false, handing all
    // logic to PvpShameleonEnterTheShadow. Base-class methods are invoked via DynamicMethod
    // stubs that emit OpCodes.Call (non-virtual) so cooldown management still runs without
    // re-entering the patched override and triggering Harmony's reentrancy guard.
    internal static class ShameleonEnterTheShadowPatch {
        private static readonly ConditionalWeakTable<ShameleonEnterTheShadowAbility, PvpShameleonEnterTheShadow> _proxies = new();

        private static Action<ChampionAbilityWithCooldown> _baseFixedUpdateCall;

        private delegate void BaseOnCharEventCall(ChampionAbilityWithCooldown self, StatsManager owner, CharacterEvent gameplayEvent, TriggerParams triggerParam);
        private static BaseOnCharEventCall _baseOnCharEventCall;

        internal static void Apply(Harmony harmony) {
            PvpShameleonEnterTheShadow.Init();

            var baseFixedUpdateMethod = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "FixedUpdateNetwork");
            if (baseFixedUpdateMethod != null) {
                var dm = new DynamicMethod(
                    "__ShameleonBaseFixedUpdate",
                    typeof(void),
                    new[] { typeof(ChampionAbilityWithCooldown) },
                    typeof(ShameleonEnterTheShadowPatch).Module,
                    skipVisibility: true);
                var il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, baseFixedUpdateMethod);
                il.Emit(OpCodes.Ret);
                _baseFixedUpdateCall = (Action<ChampionAbilityWithCooldown>)dm.CreateDelegate(typeof(Action<ChampionAbilityWithCooldown>));
            } else {
                ThePitMod.PublicLogger.LogWarning("ThePit: ChampionAbilityWithCooldown.FixedUpdateNetwork not found — Shameleon cooldown inactive in PvP.");
            }

            var baseOnCharEventMethod = AccessTools.Method(typeof(ChampionAbilityWithCooldown), "OnCharacterEvent",
                new[] { typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) })
                ?? AccessTools.Method(typeof(ChampionAbility), "OnCharacterEvent",
                    new[] { typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) });
            if (baseOnCharEventMethod != null) {
                var dm2 = new DynamicMethod(
                    "__ShameleonBaseOnCharEvent",
                    typeof(void),
                    new[] { typeof(ChampionAbilityWithCooldown), typeof(StatsManager), typeof(CharacterEvent), typeof(TriggerParams) },
                    typeof(ShameleonEnterTheShadowPatch).Module,
                    skipVisibility: true);
                var il2 = dm2.GetILGenerator();
                il2.Emit(OpCodes.Ldarg_0);
                il2.Emit(OpCodes.Ldarg_1);
                il2.Emit(OpCodes.Ldarg_2);
                il2.Emit(OpCodes.Ldarg_3);
                il2.Emit(OpCodes.Call, baseOnCharEventMethod);
                il2.Emit(OpCodes.Ret);
                _baseOnCharEventCall = (BaseOnCharEventCall)dm2.CreateDelegate(typeof(BaseOnCharEventCall));
            } else {
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
            _baseFixedUpdateCall?.Invoke(__instance);
            if (_proxies.TryGetValue(__instance, out var s)) { s.OnFixedUpdate(); }
            return false;
        }

        private static bool OnCharacterEventPrefix(ShameleonEnterTheShadowAbility __instance, StatsManager owner, CharacterEvent gameplayEvent, TriggerParams triggerParam) {
            if (!ThePitState.IsDraftMode) { return true; }
            _baseOnCharEventCall?.Invoke(__instance, owner, gameplayEvent, triggerParam);
            if (_proxies.TryGetValue(__instance, out var s)) { s.OnCharacterEvent(gameplayEvent); }
            return false;
        }

        internal static void Reset() {
            foreach (var a in UnityEngine.Object.FindObjectsOfType<ShameleonEnterTheShadowAbility>()) {
                if (_proxies.TryGetValue(a, out var s)) { s.Reset(); }
            }
        }
    }
}
