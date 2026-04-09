using System.Reflection;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.Patch.Abilities {
    internal class PvpBlazeAttackAbility {
        internal static readonly FieldInfo CasterField = AccessTools.Field(typeof(BlazeAttackAbility), "_projectileCaster");
        internal static readonly FieldInfo CasterLastField = AccessTools.Field(typeof(BlazeAttackAbility), "_projectileCasterLastShot");

        private readonly BlazeAttackAbility _inst;
        private bool _expanded;
        private ProjectileCaster _caster, _casterLast;
        private ProjectileCasterExpander.SavedMasks _saved, _savedLast;

        internal PvpBlazeAttackAbility(BlazeAttackAbility inst) { _inst = inst; }

        internal void TryExpand() {
            if (ThePitState.IsDraftMode && ThePitState.ArenaEntered) { Expand(); }
        }

        internal void Expand() {
            if (_expanded || !ProjectileCasterExpander.IsReady) { return; }
            _caster = CasterField?.GetValue(_inst) as ProjectileCaster;
            _casterLast = CasterLastField?.GetValue(_inst) as ProjectileCaster;
            if (_caster != null) { _saved = ProjectileCasterExpander.Expand(_caster); }
            if (_casterLast != null) { _savedLast = ProjectileCasterExpander.Expand(_casterLast); }
            _expanded = true;
        }

        internal void Reset() {
            if (!_expanded) { return; }
            ProjectileCasterExpander.Reset(_caster, _saved);
            ProjectileCasterExpander.Reset(_casterLast, _savedLast);
            _expanded = false;
        }
    }
}
