using System.Reflection;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    internal class PvpBeatriceEntanglingRootsAbility {
        internal static readonly FieldInfo CasterField = AccessTools.Field(typeof(BeatriceEntanglingRootAbility), "_projectileCaster");

        private readonly BeatriceEntanglingRootAbility _inst;
        private bool _expanded;
        private ProjectileCaster _caster;
        private ProjectileCasterExpander.SavedMasks _saved;

        internal PvpBeatriceEntanglingRootsAbility(BeatriceEntanglingRootAbility inst) { _inst = inst; }

        internal void TryExpand() {
            if (ThePitState.IsDraftMode && ThePitState.ArenaEntered) { Expand(); }
        }

        internal void Expand() {
            if (_expanded || !ProjectileCasterExpander.IsReady) { return; }
            _caster = CasterField?.GetValue(_inst) as ProjectileCaster;
            if (_caster != null) { _saved = ProjectileCasterExpander.Expand(_caster); }
            _expanded = true;
        }

        internal void Reset() {
            if (!_expanded) { return; }
            ProjectileCasterExpander.Reset(_caster, _saved);
            _expanded = false;
        }
    }
}
