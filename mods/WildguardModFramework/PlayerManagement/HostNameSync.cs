using Fusion;
using HarmonyLib;
using RR;

namespace WildguardModFramework.Fixes {
    // Workaround: clients never receive the host's UserName because
    // RPC_Handle_SetUserData_All fires before any client is connected.
    // When a new client joins the host re-broadcasts its own name via the same RPC.
    // Delete this file (and the Apply() call in WildguardModFrameworkMod) once the game fixes it.
    internal static class HostNameSync {
        internal static void Apply(Harmony harmony) {
            var onPlayerJoined = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerJoined));
            if (onPlayerJoined == null) {
                WmfMod.PublicLogger.LogWarning("WMF: PlayerManager.OnPlayerJoined not found — host name sync inactive.");
                return;
            }
            harmony.Patch(onPlayerJoined, postfix: new HarmonyMethod(typeof(HostNameSync), nameof(OnPlayerJoinedPostfix)));
        }

        private static void OnPlayerJoinedPostfix(PlayerManager __instance, NetworkRunner runner, PlayerRef playerRef) {
            if (!runner.IsServer || playerRef == runner.LocalPlayer) { return; }
            var local = __instance.LocalPlayer;
            if (local == null || string.IsNullOrEmpty(local.UserName)) {
                WmfMod.PublicLogger.LogWarning($"WMF: HostNameSync — local null={local == null}, name='{local?.UserName}'.");
                return;
            }
            WmfMod.PublicLogger.LogInfo($"WMF: HostNameSync — re-broadcasting '{local.UserName}' to {playerRef.PlayerId}.");
            __instance.RPC_Handle_SetUserData_All(runner.LocalPlayer, local.UserName, local.ProfileUUID);
        }
    }
}
