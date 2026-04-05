using HarmonyLib;
using RR.Backend;
using RR.Backend.Integration;
using Steamworks;

namespace PlayerNameFix.Patch {
    public static class PlayerNameFixPatch {
        public static void Apply(Harmony harmony) {
            var initializeFromMethod = AccessTools.Method(typeof(WomboPlayer), "InitializeFrom");
            if (initializeFromMethod == null) {
                PlayerNameFixMod.PublicLogger.LogWarning("PlayerNameFix: Could not find WomboPlayer.InitializeFrom — patch inactive.");
                return;
            }

            harmony.Patch(initializeFromMethod,
                postfix: new HarmonyMethod(AccessTools.Method(typeof(PlayerNameFixPatch), nameof(InitializeFromPostfix))));

            PlayerNameFixMod.PublicLogger.LogInfo("PlayerNameFix patch applied.");
        }

        private static void InitializeFromPostfix(WomboPlayer __instance) {
            if (__instance.PlayerProfile?.Name != "<N/A>") {
                return;
            }

            if (!SteamController.Initialized) {
                return;
            }

            string steamName = SteamFriends.GetPersonaName();
            if (string.IsNullOrEmpty(steamName)) {
                return;
            }

            __instance.PlayerProfile.Name = steamName;
            PlayerNameFixMod.PublicLogger.LogInfo($"PlayerNameFix: replaced <N/A> with Steam persona name \"{steamName}\".");
        }
    }
}
