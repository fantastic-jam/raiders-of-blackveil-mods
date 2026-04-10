using System.Collections.Generic;
using RR.Game;
using RR.Game.Perk;
using RR.Game.Stats;

namespace ThePit.Patch {
    internal static class ThePitPerkFilter {
        internal static bool IsBanned(PerkDescriptor perk) {
            foreach (var func in perk.GetFunctionalities()) {
                if (func.triggerEvent == CharacterEvent.OnEnemyDied) { return true; }
                if (func.ChangedEffect == StatusEffect.InstantKill) { return true; }
                if (func.ChangedEffect == StatusEffect.Taunt) { return true; }
                if (func.ChangedEffect == StatusEffect.ReviveMe) { return true; }
                if (func.ChangedProperty == Property.TauntEnabled) { return true; }
            }
            return false;
        }

        internal static void FilterResult(List<PerkDescriptor> result) {
            if (!ThePitState.IsDraftMode || result == null) { return; }
            result.RemoveAll(IsBanned);
        }
    }
}
