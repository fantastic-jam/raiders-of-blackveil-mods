using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    // In the original, FlowerEffect() calls _hitDetector.OverlapSphere() and calls
    // AddArmorPlate() on EVERY champion in range. In PvP only the caster should receive it.
    internal class PvpBeatriceSpecialObject {
        private readonly BeatriceSpecialObject _inst;

        internal PvpBeatriceSpecialObject(BeatriceSpecialObject inst) { _inst = inst; }

        // Returns false to skip the original (PvP mode active), true to run it (PvE fallback).
        internal bool FlowerEffect() {
            if (_inst.Runner?.IsServer != true) { return true; }
            _inst._charRef?.Stats?.Protection?.AddArmorPlate();
            return false;
        }
    }
}
