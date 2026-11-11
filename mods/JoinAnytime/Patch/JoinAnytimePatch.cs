using System;
using Fusion;
using HarmonyLib;
using RR;
using RR.Backend.Phoenix;

namespace JoinAnytime.Patch {
    /// <summary>
    /// Harmony wiring only — logic lives in <see cref="JoinAdmission"/>, <see cref="NewcomerFactory"/> and
    /// <see cref="LobbyListing"/>. Everything here is host-side; joining clients need no mod.
    /// </summary>
    internal static class JoinAnytimePatch {
        private static Harmony _harmony;
        private static bool _patched;
        internal static bool Disabled;

        /// <summary>Resolves reflection handles once. False = a required game member is missing; the mod stays off.</summary>
        internal static bool Init() => NewcomerFactory.Init();

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;

            // ── Admission: fabricate a Phoenix member before vanilla checks raid membership ──
            var onConnectRequest = AccessTools.Method(typeof(NetworkManager), nameof(NetworkManager.OnConnectRequest));
            if (onConnectRequest == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: NetworkManager.OnConnectRequest not found — mid-run joins will be refused by the game.");
            } else {
                harmony.Patch(onConnectRequest, prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(OnConnectRequestPrefix)));
            }

            // ── Track which Fusion player a pending newcomer is, and drop them if they leave early ──
            var onPlayerJoined = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerJoined));
            if (onPlayerJoined == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: PlayerManager.OnPlayerJoined not found — early-leaving newcomers are only dropped by timeout.");
            } else {
                harmony.Patch(onPlayerJoined, postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(OnPlayerJoinedPostfix)));
            }

            var onPlayerLeft = AccessTools.Method(typeof(PlayerManager), nameof(PlayerManager.OnPlayerLeft));
            if (onPlayerLeft == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: PlayerManager.OnPlayerLeft not found — early-leaving newcomers stay recoverable until run end.");
            } else {
                harmony.Patch(onPlayerLeft, prefix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(OnPlayerLeftPrefix)));
            }

            // ── Roll perks when the game restores the newcomer (champion + slot now exist) ──
            var tryConsume = AccessTools.Method(typeof(PhoenixManager), nameof(PhoenixManager.TryConsumeReconnectRestore));
            if (tryConsume == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: PhoenixManager.TryConsumeReconnectRestore not found — newcomers join without perks.");
            } else {
                harmony.Patch(tryConsume, postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(TryConsumeReconnectRestorePostfix)));
            }

            // ── Host tick: stay listed in the lobby browser during the run, prune stale admissions ──
            var announce = AccessTools.Method(typeof(GameManager), nameof(GameManager.AnnounceToLobbyListIfMultiplayer));
            if (announce == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: GameManager.AnnounceToLobbyListIfMultiplayer not found — the session disappears from the lobby browser once the run starts.");
            } else {
                harmony.Patch(announce, postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(AnnounceToLobbyListPostfix)));
            }

            // ── Run end / shutdown: the game clears all Phoenix state, so do we ──
            var clearPending = AccessTools.Method(typeof(PhoenixManager), nameof(PhoenixManager.ClearPendingRecovery));
            if (clearPending == null) {
                JoinAnytimeMod.PublicLogger.LogWarning("JoinAnytime: PhoenixManager.ClearPendingRecovery not found — pending newcomers are not reset between runs.");
            } else {
                harmony.Patch(clearPending, postfix: new HarmonyMethod(typeof(JoinAnytimePatch), nameof(ClearPendingRecoveryPostfix)));
            }

            JoinAnytimeMod.PublicLogger.LogInfo("JoinAnytime patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            JoinAdmission.Reset();
            _patched = false;
        }

        // ── Patch methods ─────────────────────────────────────────────────────

        private static void OnConnectRequestPrefix(NetworkRunner runner, byte[] token) =>
            JoinAdmission.OnConnectRequest(runner, token);

        private static void OnPlayerJoinedPostfix(NetworkRunner runner, PlayerRef playerRef) =>
            JoinAdmission.OnPlayerJoined(runner, playerRef);

        private static void OnPlayerLeftPrefix(PlayerRef playerRef) =>
            JoinAdmission.OnPlayerLeft(playerRef);

        private static void TryConsumeReconnectRestorePostfix(bool __result, Guid playerId, ref PhoenixPartyMember member) =>
            JoinAdmission.OnRestoreConsumed(__result, playerId, member);

        private static void AnnounceToLobbyListPostfix() =>
            JoinAnytimeOrchestrator.OnHostTick();

        private static void ClearPendingRecoveryPostfix() =>
            JoinAdmission.Reset();
    }
}
