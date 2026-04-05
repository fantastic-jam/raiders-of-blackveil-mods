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
                AccessTools.Method(typeof(BackendManager), "PlaySessionEndLevel"),
                "OfflineMode: Could not find BackendManager.PlaySessionEndLevel.",
                prefix: new HarmonyMethod(typeof(OfflineBackendPatch), nameof(PlaySessionEndLevelPrefix)));

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

        private static bool _validationDeferred;

        // Block the startup call from BackendManager.Init(); let the next call (user-triggered) through.
        private static bool StartAsyncValidateGameReleasePrefix() {
            if (!_validationDeferred) {
                _validationDeferred = true;
                return false;
            }
            return true;
        }

        private static bool StartPlaySessionPrefix(BackendManager __instance, Action<bool> callback) {
            if (!OfflineModeState.IsOffline) { return true; }

            // Restore WomboPlayer so Player.ProfileUUID gets the correct UUID on spawn.
            var savedPlayer = OfflineSaveManager.LoadWomboPlayer();
            if (savedPlayer == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: No WomboPlayer data found — profile UUID will be empty.");
            } else {
                __instance.LoggedInWomboPlayer = savedPlayer;
                OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: Restored WomboPlayer UUID={savedPlayer.WomboPlayerId}.");
            }

            // Force connection state so all guarded in-session methods (BeginLevel, EndLevel,
            // BeginRun, EndRun, SendUpdateEvent) take their local ErrorBackendUnreachable fallback
            // path and call callback(true, emptyResponse) without making any HTTP requests.
            __instance.CurrentBackendState.connection = BackendManager.BackendNetworkCondition.ErrorBackendUnreachable;

            // Set State = ActiveGameSession so EventBeginLevel's state guard passes and routes
            // through PlaySessionBeginLevel, which then hits the fallback path above.
            _stateSetter?.Invoke(__instance, new object[] { BackendManager.BackendManagerState.ActiveGameSession });

            var config = (BackendManager.WomboBackendConfiguration)_configField.GetValue(__instance);
            var fusionAppSettings = (FusionAppSettings)_buildSettingsMethod.Invoke(__instance, new object[] { null, config.FusionProductId, "1.0.0" });

            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Launching local Fusion session (GameMode.Single).");
            _ = NetworkManager.Instance.Launch(fusionAppSettings, GameMode.Single, Guid.NewGuid());
            callback?.Invoke(true);
            return false;
        }

        private static bool EndPlaySessionPrefix(BackendManager __instance, Action<bool> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            OfflineModeState.IsOffline = false;
            // Reset stale state so IsLoggedIn() returns false and the next play button click
            // correctly triggers the deferred login flow instead of attempting an unauthed session.
            _stateSetter?.Invoke(__instance, new object[] { BackendManager.BackendManagerState.Initialized });
            __instance.CurrentBackendState.connection = BackendManager.BackendNetworkCondition.NotInitialized;
            callback?.Invoke(true);
            return false;
        }

        // The game's ErrorBackendUnreachable fallback for PlaySessionEndLevel calls callback(true, null),
        // which causes a NullReferenceException in MetricsManager.SendCurrentLevelReport.
        private static bool PlaySessionEndLevelPrefix(
            IngressMessagePlaySessionEndLevel requestEndLevel,
            Action<bool, IngressResponsePlaySessionEndLevel> callback) {
            if (!OfflineModeState.IsOffline) { return true; }
            callback?.Invoke(true, new IngressResponsePlaySessionEndLevel());
            return false;
        }

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

            // Online: keep WomboPlayer data current for future offline use.
            OfflineSaveManager.SaveWomboPlayer(BackendManager.Instance?.LoggedInWomboPlayer);

            // If the local save timestamp matches the backend state, the user played
            // offline since last sync — back up the backend state before the game overwrites it.
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
