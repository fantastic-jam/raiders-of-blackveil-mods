using System.Reflection;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpBlazeAttackAbility {
        internal static FieldInfo CasterField;
        internal static FieldInfo CasterLastField;

        private readonly BlazeAttackAbility _inst;
        private bool _expanded;
        private ProjectileCaster _caster, _casterLast;
        private ProjectileCasterExpander.SavedMasks _saved, _savedLast;

        internal static void Init() {
            CasterField = AccessTools.Field(typeof(BlazeAttackAbility), "_projectileCaster");
            if (CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeAttackAbility._projectileCaster not found — Blaze attack PvP inactive.");
            }

            CasterLastField = AccessTools.Field(typeof(BlazeAttackAbility), "_projectileCasterLastShot");
            if (CasterLastField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BlazeAttackAbility._projectileCasterLastShot not found — Blaze last-shot PvP inactive.");
            }
        }

        internal PvpBlazeAttackAbility(BlazeAttackAbility inst) {
            _inst = inst;
            // Expand immediately if arena is already active (e.g. spawned mid-match).
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
