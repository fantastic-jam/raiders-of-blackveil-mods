using Xunit;

namespace RaiderRoughPatches.Tests {

    // ── Pure extraction of DoorManager.CheckVotes ─────────────────────────
    //
    // DoorManager is a NetworkBehaviour — untestable directly. This file
    // extracts the exact algorithm so we can run it in isolation and prove
    // bugs exist before patching the game.
    //
    // Source: game-src/RR.Level/DoorManager.cs  CheckVotes() lines 654-677

    internal sealed class VoteEngine {
        // Mirrors DoorManager.PlayerVotes[] — index 0-2, -1 means "not voted"
        private readonly int[] _votes = { -1, -1, -1 };
        private int _playerCount;

        public int? ResolvedVote { get; private set; }

        internal VoteEngine(int playerCount) => _playerCount = playerCount;

        // Called by RPC_VoteState on the server (slot = player's SlotIndex).
        internal void CastVote(int slot, int door) {
            _votes[slot] = door;
            CheckVotes();           // exact game call site
        }

        // Called when a player disconnects.
        // BUG 1: game does NOT call CheckVotes here.
        // BUG 2: the game also does NOT clear the disconnected player's slot,
        //        so their stale vote may still influence the (uncalled) check.
        internal void PlayerDisconnected(int slot) {
            _playerCount--;
            // CheckVotes() intentionally omitted — mirrors game exactly.
        }

        // Exact copy of DoorManager.CheckVotes() — do not simplify.
        private void CheckVotes() {
            switch (_playerCount) {
                case 1:
                    if (_votes[0] > -1) {
                        ResolvedVote = _votes[0];           // hardcoded slot 0 only
                    }

                    break;
                case 2:
                    if (_votes[0] > -1 && _votes[0] == _votes[1]) {
                        ResolvedVote = _votes[0];           // hardcoded slots 0 & 1 only
                    }

                    break;
                case 3:
                    if (_votes[0] > -1 && _votes[0] == _votes[1] && _votes[0] == _votes[2]) {
                        ResolvedVote = _votes[0];
                    }

                    break;
            }
        }
    }

    // ── Proposed fix ──────────────────────────────────────────────────────
    //
    // Three changes to CheckVotes:
    //   1. Clear the disconnecting player's vote slot on disconnect.
    //   2. Re-evaluate on disconnect (call CheckVotes after PlayerDisconnected).
    //   3. Scan all slots instead of hardcoding 0/1/2 per player count.
    //
    // The mod implements this via a shadow vote dictionary that tracks active
    // player votes independently of the game's NetworkArray<int> PlayerVotes.

    internal sealed class FixedVoteEngine {
        private readonly int[] _votes = { -1, -1, -1 };
        private int _playerCount;

        public int? ResolvedVote { get; private set; }

        internal FixedVoteEngine(int playerCount) => _playerCount = playerCount;

        internal void CastVote(int slot, int door) {
            _votes[slot] = door;
            CheckVotes();
        }

        internal void PlayerDisconnected(int slot) {
            _votes[slot] = -1;      // FIX 1: clear disconnected player's stale vote
            _playerCount--;
            CheckVotes();           // FIX 2: re-evaluate after every disconnect
        }

        // FIX 3: scan all slots; require exactly playerCount non-(-1) votes all agreeing.
        private void CheckVotes() {
            int? agreedDoor = null;
            int voteCount = 0;
            for (int i = 0; i < 3; i++) {
                if (_votes[i] < 0) { continue; }

                voteCount++;
                if (agreedDoor == null) {
                    agreedDoor = _votes[i];
                } else if (agreedDoor.Value != _votes[i]) {
                    return;     // disagreement — wait
                }
            }

            if (voteCount == _playerCount && agreedDoor.HasValue) {
                ResolvedVote = agreedDoor;
            }
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────

    public class DoorVoteTests {

        // ── Baseline (working paths) ───────────────────────────────────────

        [Fact]
        public void TwoPlayers_BothVoteSameDoor_Resolves() {
            var engine = new VoteEngine(playerCount: 2);
            engine.CastVote(slot: 0, door: 0);
            engine.CastVote(slot: 1, door: 0);
            Assert.Equal(0, engine.ResolvedVote);
        }

        [Fact]
        public void TwoPlayers_VoteDifferentDoors_DoesNotResolve() {
            var engine = new VoteEngine(playerCount: 2);
            engine.CastVote(slot: 0, door: 0);
            engine.CastVote(slot: 1, door: 1);
            Assert.Null(engine.ResolvedVote);
        }

        [Fact]
        public void OnePlayer_InSlotZero_VotesAndResolves() {
            var engine = new VoteEngine(playerCount: 1);
            engine.CastVote(slot: 0, door: 2);
            Assert.Equal(2, engine.ResolvedVote);
        }

        // ── Bug: 2-player — slot-1 player is locked when slot-0 hasn't voted ─
        //
        // Scenario reported:
        //   • 2-player session; user is in slot 1.
        //   • User votes (slot 1). Vote is pending — slot 0 hasn't voted yet.
        //   • Slot-0 player disconnects. PlayerCount drops to 1.
        //   • CheckVotes never re-called → slot 1's vote never evaluated → stuck.

        [Fact]
        public void Bug_TwoPlayer_Slot1Voted_Slot0Leaves_VoteNeverResolves() {
            var engine = new VoteEngine(playerCount: 2);

            engine.CastVote(slot: 1, door: 0);
            engine.PlayerDisconnected(slot: 0);

            // case 1: votes[0] > -1 → false (slot 0 was -1) → stuck
            Assert.Null(engine.ResolvedVote);
        }

        // ── Bug: 3-player — slot-1 leaves, slots 0 and 2 can't resolve ───────
        //
        // Scenario clarified by the user:
        //   • 3-player session; slot 0 = host, slots 1 and 2 = clients.
        //   • Slots 0 and 2 both vote door 0. Slot 1 disconnects without voting.
        //   • PlayerCount drops to 2.
        //   • case 2 checks votes[0] AND votes[1] (hardcoded) — ignores slot 2.
        //   • votes[1] = -1 (never voted) → case 2 fails → stuck.

        [Fact]
        public void Bug_ThreePlayer_Slot1Leaves_Slots0And2Voted_VoteNeverResolves() {
            var engine = new VoteEngine(playerCount: 3);

            engine.CastVote(slot: 0, door: 0);
            engine.CastVote(slot: 2, door: 0);
            engine.PlayerDisconnected(slot: 1);     // middle player leaves, never voted

            // case 2: votes[0] > -1 && votes[0] == votes[1] → 0 > -1 && 0 == -1 → false
            Assert.Null(engine.ResolvedVote);
        }

        [Fact]
        public void Bug_ThreePlayer_Slot1LeavesBeforeSlot2Votes_Stuck() {
            var engine = new VoteEngine(playerCount: 3);

            engine.CastVote(slot: 0, door: 0);
            engine.PlayerDisconnected(slot: 1);     // leaves mid-vote
            engine.CastVote(slot: 2, door: 0);      // slot 2 votes after

            // After disconnect: playerCount=2. CastVote(slot:2) calls CheckVotes.
            // case 2: votes[0]=0, votes[1]=-1 → 0!=-1 → not resolved.
            Assert.Null(engine.ResolvedVote);
        }

        // ── Fix: all disconnect + slot-gap scenarios resolve correctly ────────

        [Fact]
        public void Fix_TwoPlayer_Slot1Voted_Slot0Leaves_Resolves() {
            var engine = new FixedVoteEngine(playerCount: 2);

            engine.CastVote(slot: 1, door: 0);
            engine.PlayerDisconnected(slot: 0);

            Assert.Equal(0, engine.ResolvedVote);
        }

        [Fact]
        public void Fix_ThreePlayer_Slot1Leaves_Slots0And2Voted_Resolves() {
            var engine = new FixedVoteEngine(playerCount: 3);

            engine.CastVote(slot: 0, door: 0);
            engine.CastVote(slot: 2, door: 0);
            engine.PlayerDisconnected(slot: 1);

            Assert.Equal(0, engine.ResolvedVote);
        }

        [Fact]
        public void Fix_ThreePlayer_Slot1LeavesBeforeSlot2Votes_Resolves() {
            var engine = new FixedVoteEngine(playerCount: 3);

            engine.CastVote(slot: 0, door: 0);
            engine.PlayerDisconnected(slot: 1);
            engine.CastVote(slot: 2, door: 0);

            Assert.Equal(0, engine.ResolvedVote);
        }

        [Fact]
        public void Fix_ThreePlayer_Slot1VotedThenLeft_Slot2VotesDifferentDoor_NoResolution() {
            var engine = new FixedVoteEngine(playerCount: 3);

            engine.CastVote(slot: 0, door: 0);
            engine.CastVote(slot: 1, door: 1);      // slot 1 voted differently
            engine.PlayerDisconnected(slot: 1);     // slot 1 leaves — their vote cleared
            engine.CastVote(slot: 2, door: 0);      // slots 0 and 2 agree on 0

            Assert.Equal(0, engine.ResolvedVote);   // slot 1's stale vote does not block
        }

        [Fact]
        public void Fix_Slot2PlayerVoted_BothOthersLeave_Resolves() {
            var engine = new FixedVoteEngine(playerCount: 3);

            engine.CastVote(slot: 2, door: 1);
            engine.PlayerDisconnected(slot: 0);
            engine.PlayerDisconnected(slot: 1);

            Assert.Equal(1, engine.ResolvedVote);
        }

        [Fact]
        public void Fix_NobodyVotedYet_PlayerLeaves_DoesNotResolveEarly() {
            var engine = new FixedVoteEngine(playerCount: 2);

            engine.PlayerDisconnected(slot: 0);

            Assert.Null(engine.ResolvedVote);
        }
    }
}
