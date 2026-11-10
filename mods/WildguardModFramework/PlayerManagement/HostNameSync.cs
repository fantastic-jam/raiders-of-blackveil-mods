using System;
using System.Reflection;
using Fusion;
using HarmonyLib;
using RR;

namespace WildguardModFramework.Fixes {
    // Workaround: clients never receive the host's UserName because
    // RPC_Handle_SetUserData_All fires before any client is connected.
    // When a new client joins the host re-broadcasts the full roster via the same RPC.
    // Since game 0.6.5 the host already re-broadcasts the roster after each
    // RPC_NotifySA_PlayerJoinedPlaySession, so this is likely obsolete — confirm in-game, then
    // delete this file (and the Apply() call in WildguardModFrameworkMod).
    internal static class HostNameSync {
        // Clients clear their raid roster on receipt, so the payload must be the full roster,
        // never just the host — reuse the game's own collector.
        private static MethodInfo _collectRaidInfo;

        internal static void Apply(Harmony harmony) {
            _collectRaidInfo = AccessTools.Method(typeof(PlayerManager), "CollectRaidInfoForBroadcast");
            if (_collectRaidInfo == null) {
                WmfMod.PublicLogger.LogWarning("WMF: PlayerManager.CollectRaidInfoForBroadcast not found — host name sync inactive.");
                return;
            }

            var onPlayerJoined = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerJoined));
            if (onPlayerJoined == null) {
                WmfMod.PublicLogger.LogWarning("WMF: PlayerManager.OnPlayerJoined not found — host name sync inactive.");
                return;
            }
            harmony.Patch(onPlayerJoined, postfix: new HarmonyMethod(typeof(HostNameSync), nameof(OnPlayerJoinedPostfix)));
        }

        private static void OnPlayerJoinedPostfix(PlayerManager __instance, NetworkRunner runner, PlayerRef playerRef) =>
            Rebroadcast(__instance, runner, playerRef);

        private static void Rebroadcast(PlayerManager manager, NetworkRunner runner, PlayerRef playerRef) {
            if (!runner.IsServer || playerRef == runner.LocalPlayer) { return; }
            var local = manager.LocalPlayer;
            if (local == null || string.IsNullOrEmpty(local.UserName)) {
                WmfMod.PublicLogger.LogWarning($"WMF: HostNameSync — local null={local == null}, name='{local?.UserName}'.");
                return;
            }

            var args = new object[5];
            _collectRaidInfo.Invoke(manager, args);
            WmfMod.PublicLogger.LogInfo($"WMF: HostNameSync — re-broadcasting roster (host '{local.UserName}') to {playerRef.PlayerId}.");
            manager.RPC_Handle_SetUserData_All(
                (string[])args[0], (PlayerRef[])args[1], (Guid[])args[2], (int[])args[3], (NetworkBool[])args[4]);
        }
    }
}
