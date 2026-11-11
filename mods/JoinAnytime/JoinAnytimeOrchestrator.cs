using RR;

namespace JoinAnytime {
    internal static class JoinAnytimeOrchestrator {
        /// <summary>
        /// Runs from <c>GameManager.AnnounceToLobbyListIfMultiplayer</c>, called every network tick
        /// (<c>GameManager.FixedUpdateNetwork</c>) on all peers — host only past the guard.
        /// </summary>
        internal static void OnHostTick() {
            if (NetworkManager.Instance?.NetworkRunner?.IsServer != true) { return; }
            LobbyListing.KeepListedDuringRun();
            JoinAdmission.PruneStale();
        }
    }
}
