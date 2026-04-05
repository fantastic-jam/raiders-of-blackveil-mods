using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fusion;
using Fusion.Photon.Realtime;
using HarmonyLib;
using RR;
using RR.Backend;
using RR.Backend.API.V1.Ingress.Message;
using UnityEngine;

namespace OfflineMode.Patch {
    public static class OfflineBackendPatch {
        private static MethodInfo _stateSetter;
        private static FieldInfo _configField;
        private static MethodInfo _buildSettingsMethod;

        public static void Apply(Harmony harmony) {
            void Patch(MethodBase method, string warning, HarmonyMethod prefix = null, HarmonyMethod postfix = null) {
                if (method == null) { OfflineModeMod.PublicLogger.LogWarning(warning); return; }
                harmony.Patch(method, prefix: prefix, postfix: postfix);
            }

            // Skip the product-release HTTP validation on startup; let user-triggered calls through.
            Patch(
                AccessTools.Method(typeof(BackendManager), "StartAsyncValidateGameRelease"),
                "OfflineMode: Could not find BackendManager.StartAsyncValidateGameRelease — startup HTTP call not suppressed.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(StartAsyncValidateGameReleasePrefix)));

            _stateSetter = AccessTools.PropertySetter(typeof(BackendManager), "State");
            if (_stateSetter == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: Could not find BackendManager.State setter.");
            }

            _configField = AccessTools.Field(typeof(BackendManager), "_brandIronConfiguration");
            _buildSettingsMethod = AccessTools.Method(typeof(BackendManager), "BuildCustomFusionAppSetting");

            var startPlaySession = AccessTools.Method(typeof(BackendManager), "StartPlaySession");
            if (startPlaySession == null || _configField == null || _buildSettingsMethod == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: Could not find BackendManager.StartPlaySession or required reflection members — offline launch inactive.");
            } else {
                harmony.Patch(startPlaySession,
                    prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(StartPlaySessionPrefix)));
            }

            Patch(
                AccessTools.Method(typeof(BackendManager), "EndPlaySession"),
                "OfflineMode: Could not find BackendManager.EndPlaySession.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(EndPlaySessionPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "PlaySessionBeginLevel"),
                "OfflineMode: Could not find BackendManager.PlaySessionBeginLevel.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(PlaySessionBeginLevelPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "PlaySessionEndLevel"),
                "OfflineMode: Could not find BackendManager.PlaySessionEndLevel.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(PlaySessionEndLevelPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "PlaySessionBeginRun"),
                "OfflineMode: Could not find BackendManager.PlaySessionBeginRun.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(PlaySessionBeginRunPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "PlaySessionEndRun"),
                "OfflineMode: Could not find BackendManager.PlaySessionEndRun.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(PlaySessionEndRunPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "SendPlaySessionupdateEvent"),
                "OfflineMode: Could not find BackendManager.SendPlaySessionupdateEvent.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(SendPlaySessionUpdateEventPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "SavePlayerGameStates"),
                "OfflineMode: Could not find BackendManager.SavePlayerGameStates.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(SavePlayerGameStatesPrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "LoadPlayerGameState"),
                "OfflineMode: Could not find BackendManager.LoadPlayerGameState.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(LoadPlayerGameStatePrefix)));

            Patch(
                AccessTools.Method(typeof(BackendManager), "Logout"),
                "OfflineMode: Could not find BackendManager.Logout.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(LogoutPrefix)));

            // In GameMode.Single the Fusion RPC authority check returns early before
            // SavePlayerGameStateLocally() is reached — bypass it when offline.
            Patch(
                AccessTools.Method(typeof(PlayerManager), "RPC_SavePlayerGameStateLocally"),
                "OfflineMode: Could not find PlayerManager.RPC_SavePlayerGameStateLocally — local save during offline runs may not work.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(RpcSavePlayerGameStateLocallyPrefix)));
        }

        private static bool StartAsyncValidateGameReleasePrefix() => LoginManager.ShouldAllowValidateRelease();

        private static bool StartPlaySessionPrefix(BackendManager __instance, Action<bool> callback) {
            if (!OfflineModeState.IsOffline) { return true; }

            // Restore WomboPlayer so Player.ProfileUUID gets the correct UUID on spawn.
            var player = LoginManager.GetWomboPlayer();
            if (player == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: No WomboPlayer data found — profile UUID will be empty.");
            } else {
                __instance.LoggedInWomboPlayer = player;
                OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: Restored WomboPlayer UUID={player.WomboPlayerId}.");
            }

            // Set State = ActiveGameSession so EventBeginLevel's state guard passes.
            _stateSetter?.Invoke(__instance, new object[] { BackendManager.BackendManagerState.ActiveGameSession });

            var config = (BackendManager.WomboBackendConfiguration)_configField.GetValue(__instance);
            var fusionAppSettings = (FusionAppSettings)_buildSettingsMethod.Invoke(__instance, new object[] { null, config.FusionProductId, "1.0.0" });

            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Launching local Fusion session (GameMode.Single).");
            _ = NetworkManager.Instance.Launch(fusionAppSettings, GameMode.Single, Guid.NewGuid());
            callback?.Invoke(true);
            return false;
        }

        private static bool EndPlaySessionPrefix(Action<bool> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            callback?.Invoke(true);
            return false;
        }

        private static bool PlaySessionBeginLevelPrefix(Action<bool, IngressResponsePlaySessionBeginLevel> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            callback?.Invoke(true, new IngressResponsePlaySessionBeginLevel());
            return false;
        }

        private static bool PlaySessionEndLevelPrefix(
            IngressMessagePlaySessionEndLevel requestEndLevel,
            Action<bool, IngressResponsePlaySessionEndLevel> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            callback?.Invoke(true, new IngressResponsePlaySessionEndLevel());
            return false;
        }

        private static bool PlaySessionBeginRunPrefix(Action<bool, IngressResponsePlaySessionBeginRun> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            callback?.Invoke(true, null);
            return false;
        }

        private static bool PlaySessionEndRunPrefix(Action<bool, IngressResponsePlaySessionEndRun> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            callback?.Invoke(true, new IngressResponsePlaySessionEndRun());
            return false;
        }

        private static bool SendPlaySessionUpdateEventPrefix() => !OfflineModeState.IsOffline;

        private static bool SavePlayerGameStatesPrefix(
            Action backendRequestCompleted,
            ref IEnumerator<WaitForSeconds> __result) {
            if (!OfflineModeState.IsOffline) { return true; }
            // Offline: skip the backend save — the game saves locally on its own.
            backendRequestCompleted?.Invoke();
            __result = Enumerable.Empty<WaitForSeconds>().GetEnumerator();
            return false;
        }

        private static bool LoadPlayerGameStatePrefix(
            Guid playerUUID,
            ref Action<Guid, PlayerGameState> callback,
            ref IEnumerator<WaitForSeconds> __result) {
            if (OfflineModeState.IsOffline) {
                var state = OfflineSaveManager.LoadGameLocalSave(playerUUID) ?? new PlayerGameState(playerUUID);
                callback?.Invoke(playerUUID, state);
                __result = Enumerable.Empty<WaitForSeconds>().GetEnumerator();
                return false;
            }

            // If the user played offline since last sync, back up the backend state before the game overwrites it.
            var localState = OfflineSaveManager.LoadGameLocalSave(playerUUID);
            if (localState == null) { return true; }

            var original = callback;
            callback = (uuid, onlineState) => {
                if (onlineState != null && onlineState.HasTimeStamp()) {
                    OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: Loading backend state (backend={onlineState.TimeStamp}, local={localState.TimeStamp}) — backing up backend state.");
                    OfflineSaveManager.SaveBackup(onlineState);
                }
                original?.Invoke(uuid, onlineState);
            };
            return true;
        }

        private static bool LogoutPrefix() => !OfflineModeState.IsOffline;

        private static bool RpcSavePlayerGameStateLocallyPrefix(PlayerManager __instance) {
            if (!OfflineModeState.IsOffline) { return true; }
            __instance.SavePlayerGameStateLocally();
            return false;
        }
    }
}
