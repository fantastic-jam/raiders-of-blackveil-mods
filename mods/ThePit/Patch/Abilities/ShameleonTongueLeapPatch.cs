using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.Patch.Abilities {
    // Expands tongueHitMask to include the Player layer so the tongue physically
    // connects to other champions. Jump-behind fires via the existing wall-jump code.
    // Stun for champions is not implemented (requires HitInfo struct internals).
    internal static class ShameleonTongueLeapPatch {
        private static LayerMask _savedMask;

        internal static void Apply(Harmony harmony) {
            var fun = AccessTools.Method(typeof(ShameleonTongueLeapAbility), "FixedUpdateNetwork");
            if (fun == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: ShameleonTongueLeapAbility.FixedUpdateNetwork not found — Tongue Leap PvP inactive.");
                return;
            }
            harmony.Patch(fun,
                prefix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(FunPrefix)),
                postfix: new HarmonyMethod(typeof(ShameleonTongueLeapPatch), nameof(FunPostfix)));
        }

        private static void FunPrefix(ShameleonTongueLeapAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            _savedMask = __instance.tongueHitMask;
            __instance.tongueHitMask = (LayerMask)(__instance.tongueHitMask.value | LayerMask.GetMask("Player"));
        }

        private static void FunPostfix(ShameleonTongueLeapAbility __instance) {
            if (!ThePitState.IsDraftMode || !ThePitState.ArenaEntered) { return; }
            __instance.tongueHitMask = _savedMask;
        }
    }
}
