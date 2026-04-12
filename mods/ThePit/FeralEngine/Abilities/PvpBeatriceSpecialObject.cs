using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // In the original, FlowerEffect() calls _hitDetector.OverlapSphere() and calls
    // AddArmorPlate() on EVERY champion in range. In PvP only the caster should receive it.
    //
    // Also: playerCheckCol has a physics Collider that Beatrice's own projectiles would hit,
    // consuming them before they reach their target. FlowerEffect() only reads
    // playerCheckCol.transform.localScale for the overlap radius — it does not need the
    // Collider to be active — so we disable it on spawn in draft mode.
    internal class PvpBeatriceSpecialObject {
        private readonly BeatriceSpecialObject _inst;

        internal PvpBeatriceSpecialObject(BeatriceSpecialObject inst) {
            _inst = inst;
            DisablePlayerCheckCollider();
        }

        // Returns false to skip the original (PvP mode active on server), true to run it (PvE fallback).
        internal bool FlowerEffect() {
            if (!ThePitState.IsDraftMode) { return true; }
            if (_inst.Runner?.IsServer != true) { return true; }
            _inst._charRef?.Stats?.Protection?.AddArmorPlate();
            return false;
        }

        private void DisablePlayerCheckCollider() {
            if (!ThePitState.IsDraftMode) { return; }
            var col = _inst.playerCheckCol?.GetComponent<Collider>();
            if (col != null) { col.enabled = false; }
        }
    }
}
