using System.Reflection;
using HarmonyLib;
using RR.Game.Character;

namespace ThePit.FeralEngine.Abilities {
    internal class PvpBeatriceAttackAbility {
        internal static FieldInfo CasterField;

        internal static void Init() {
            CasterField = AccessTools.Field(typeof(BeatriceAttackAbility), "_projectileCaster");
            if (CasterField == null) {
                ThePitMod.PublicLogger.LogWarning("ThePit: BeatriceAttackAbility._projectileCaster not found — Beatrice attack PvP inactive.");
            }
        }

        private readonly BeatriceAttackAbility _inst;
        private bool _expanded;
        private ProjectileCaster _caster;
        private ProjectileCasterExpander.SavedMasks _saved;

        internal PvpBeatriceAttackAbility(BeatriceAttackAbility inst) {
            _inst = inst;
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
