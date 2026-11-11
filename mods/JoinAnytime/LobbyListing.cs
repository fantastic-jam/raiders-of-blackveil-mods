using Cysharp.Threading.Tasks;
using Fusion;
using RR;
using RR.Backend.Phoenix;

namespace JoinAnytime {
    /// <summary>
    /// Keeps the session in the lobby browser during a run. Vanilla delists it at run start
    /// (<c>GameManager.RPC_RunStart</c> → <c>RemoveFromLobbyListAsync</c>) and only re-announces while
    /// <c>State == Lobby</c>; the listing has a backend TTL, so we keep announcing (throttled to 6 s by
    /// <c>JoinableSessions.AnnounceToLobbyListAsync</c>) while a newcomer could actually be admitted.
    /// </summary>
    internal static class LobbyListing {
        internal static void KeepListedDuringRun() {
            var gm = GameManager.Instance;
            var pm = PlayerManager.Instance;
            var backend = BackendManager.Instance;
            if (gm == null || pm == null || backend == null) { return; }
            if (NetworkManager.Instance?.FusionGameMode != GameMode.Host) { return; }
            if (gm.State != GameManager.GameState.Dungeon) { return; }          // vanilla covers the lobby; tutorial has no checkpoint
            if (pm.GetPlayers().Count >= 3) { return; }                         // full — let the listing expire
            if (PhoenixManager.CurrentRunReconnect == null) { return; }         // nothing to admit into yet

            var session = backend.CurrentJoinablePlaySession;
            if (session == null || session.Id == System.Guid.Empty) { return; }
            backend._sessionManager?.BackendSessionContext?.JoinableSessions?.AnnounceToLobbyListAsync(session).Forget();
        }
    }
}
