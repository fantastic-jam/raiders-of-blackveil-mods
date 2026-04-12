using RR.Game;
using RR.Game.Perk;

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

        // Postfix on PerkDescriptor.IsItUnlocked — marks banned perks as unavailable
        // before they enter the selection pool, so GetRandomPerkAmount always draws
        // a full set (no null shrine slots, no UI freeze).
        internal static void FilterUnlocked(PerkDescriptor perk, ref bool result) {
            if (!result || !ThePitState.IsDraftMode) { return; }
            if (IsBanned(perk)) { result = false; }
        }
    }
}
