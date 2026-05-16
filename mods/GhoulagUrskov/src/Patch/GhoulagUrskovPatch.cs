using System.Reflection;
using HarmonyLib;
using RR.Game.Character;

namespace GhoulagUrskov.Patch {
    internal static class GhoulagUrskovPatch {
        private static Harmony _harmony;
        private static bool _patched;
        internal static bool Disabled;

        internal static bool Init() {
            return GhoulagUrskovAssets.Load();
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) {
                return;
            }

            _harmony = harmony;

            var awakeMethod = AccessTools.Method(typeof(NetworkCharacterBase), "Awake");
            if (awakeMethod == null) {
                GhoulagUrskovMod.PublicLogger.LogError("[GhoulagUrskov] Could not find NetworkCharacterBase.Awake — patch unavailable.");
                return;
            }
            harmony.Patch(awakeMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(GhoulagUrskovPatch), nameof(OnCharacterAwake))));

            GhoulagUrskovMod.PublicLogger.LogInfo("GhoulagUrskov patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }

        private static void OnCharacterAwake(NetworkCharacterBase __instance) {
            GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] OnCharacterAwake: {__instance.GetType().Name}");
            if (__instance is NetworkChampionRhino rhino) {
                GhoulagUrskovMeshSwapper.Swap(rhino);
            }
        }
    }
}
