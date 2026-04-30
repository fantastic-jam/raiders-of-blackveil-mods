using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RaiderRoughPatches.Tests {

    // ── Pure extraction of AreaCharacterSelector.SelectCandidates ────────────
    //
    // AreaCharacterSelector is a NetworkBehaviour — untestable directly. This
    // file extracts the candidate-selection algorithm to prove the self-grant
    // bug before patching.
    //
    // Source: game-src/RR.Game.Perk/AreaCharacterSelector.cs
    //   SelectCandidates()  lines 1349-1364  (ally iteration + _chooseOwnerLast removal)
    //   DoAreaSelection()   lines 1230-1299  (outer while; single-pass because
    //                                         _multipleSelectionEnabled=false when eachTargetOnce=true)
    //
    // Relevant defaults:
    //   AreaCharacterSelector line 195: _chooseOwnerLast = true
    //   PerkFunctionality     line 225: eachTargetOnce   = true  → _multipleSelectionEnabled = false
    //   PerkFunctionality     line 227: canSelectMyself  = true  → _ignoreAuraOwner = false
    //
    // Bug claim (RepulsiveRaccoon): every perk that grants a status effect to
    // "self and nearest ally" only grants it to the ally in multiplayer. In
    // single-player the caster does receive it.
    //
    // Root cause: when _chooseOwnerLast=true and there is at least one other
    // living ally, SelectCandidates *removes* the owner from candidates entirely
    // (lines 1360-1364). It does not defer them — it eliminates them. The outer
    // while loop in DoAreaSelection never gets a second chance to re-add the owner
    // because it breaks after the first SelectCandidates pass (_multipleSelectionEnabled=false).
    // In solo play the condition "candidates.Count > 1" is never true, so the owner
    // survives and is selected.

    internal sealed class AllyTargetSelector {
        // Mirrors SelectCandidates (ally branch) + single-pass DoAreaSelection.
        // _chooseOwnerLast is baked as true — it is the game's hardcoded default.
        // Geometry/distance are not modelled; only candidate inclusion matters for this bug.
        public List<int> SelectTargets(IReadOnlyList<int> alivePlayers, int owner, int targetLimit) {
            var candidates = new List<int>(alivePlayers);

            // lines 1360-1364: owner removed whenever there is more than one candidate
            // BUG: this excludes the owner entirely, not "last"
            if (candidates.Count > 1) {
                candidates.RemoveAll(c => c == owner);
            }

            // DoAreaSelection: single pass — _multipleSelectionEnabled=false causes
            // an immediate break after the first SelectCandidates round
            return candidates.Take(targetLimit).ToList();
        }
    }

    // ── Proposed fix ─────────────────────────────────────────────────────────
    //
    // _chooseOwnerLast is intended to mean "prefer other targets, fall back to
    // the caster". The fix: defer the owner rather than discard them. After
    // selecting from the non-owner candidate pool, if there are still unfilled
    // target slots, append the owner.

    internal sealed class FixedAllyTargetSelector {
        public List<int> SelectTargets(IReadOnlyList<int> alivePlayers, int owner, int targetLimit) {
            var candidates = new List<int>(alivePlayers);
            bool ownerDeferred = false;

            // FIX: track deferral instead of discarding
            if (candidates.Count > 1) {
                candidates.RemoveAll(c => c == owner);
                ownerDeferred = true;
            }

            // FIX: reserve one slot for the deferred owner so allies don't consume all slots
            int allySlots = ownerDeferred ? targetLimit - 1 : targetLimit;
            var targets = candidates.Take(allySlots).ToList();

            // FIX: fill the reserved slot with the deferred owner
            if (ownerDeferred && alivePlayers.Contains(owner)) {
                targets.Add(owner);
            }

            return targets;
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    public class BarrierSelfGrantTests {

        // Barrier perk targets up to 2 actors: self + nearest ally.
        private const int Owner = 0;
        private const int Ally1 = 1;
        private const int Ally2 = 2;
        private const int TargetLimit = 2;

        // ── Baseline: solo behaves correctly ──────────────────────────────────

        [Fact]
        public void Solo_OnePlayer_OwnerReceivesEffect() {
            var selector = new AllyTargetSelector();
            var players = new[] { Owner };

            var targets = selector.SelectTargets(players, Owner, TargetLimit);

            Assert.Contains(Owner, targets);
        }

        // ── Bug: self excluded in multiplayer ─────────────────────────────────
        //
        // Scenario: 2-player run. Caster triggers a "self + nearest ally" perk.
        // candidates = [Owner, Ally1]; _chooseOwnerLast removes Owner → [Ally1].
        // targetLimit=2 never satisfied; Owner never buffed.

        [Fact]
        public void Bug_TwoPlayer_OwnerExcluded_OnlyAllyReceivesEffect() {
            var selector = new AllyTargetSelector();
            var players = new[] { Owner, Ally1 };

            var targets = selector.SelectTargets(players, Owner, TargetLimit);

            Assert.DoesNotContain(Owner, targets);   // Owner never buffed — the bug
            Assert.Contains(Ally1, targets);          // Ally is buffed correctly
        }

        // ── Bug: owner displaced by second ally in 3-player run ───────────────
        //
        // Scenario: 3-player run, targetLimit=2. After removing Owner, candidates
        // = [Ally1, Ally2]. Both ally slots are filled; Owner is never a candidate.

        [Fact]
        public void Bug_ThreePlayer_OwnerExcluded_BothSlotsFilledByAllies() {
            var selector = new AllyTargetSelector();
            var players = new[] { Owner, Ally1, Ally2 };

            var targets = selector.SelectTargets(players, Owner, TargetLimit);

            Assert.Equal(2, targets.Count);
            Assert.DoesNotContain(Owner, targets);
            Assert.Contains(Ally1, targets);
            Assert.Contains(Ally2, targets);
        }

        // ── Fix: owner included after allies are selected ─────────────────────

        [Fact]
        public void Fix_TwoPlayer_OwnerIncludedAfterAllySelected() {
            var selector = new FixedAllyTargetSelector();
            var players = new[] { Owner, Ally1 };

            var targets = selector.SelectTargets(players, Owner, TargetLimit);

            Assert.Equal(2, targets.Count);
            Assert.Contains(Owner, targets);
            Assert.Contains(Ally1, targets);
            // Owner is deferred (chosen last): ally is at index 0
            Assert.Equal(Ally1, targets[0]);
            Assert.Equal(Owner, targets[1]);
        }

        [Fact]
        public void Fix_ThreePlayer_NearestAllyAndOwnerSelected_SecondAllyDropped() {
            var selector = new FixedAllyTargetSelector();
            var players = new[] { Owner, Ally1, Ally2 };

            // targetLimit=2 means: 1 ally slot + 1 deferred-owner slot
            var targets = selector.SelectTargets(players, Owner, TargetLimit);

            Assert.Equal(2, targets.Count);
            Assert.Contains(Owner, targets);
            // Only one ally can fit; Owner fills the second slot
            Assert.Single(targets.Where(t => t != Owner));
        }

        [Fact]
        public void Fix_Solo_OnePlayer_OwnerStillReceivesEffect() {
            var selector = new FixedAllyTargetSelector();
            var players = new[] { Owner };

            var targets = selector.SelectTargets(players, Owner, TargetLimit);

            Assert.Contains(Owner, targets);
        }
    }
}
