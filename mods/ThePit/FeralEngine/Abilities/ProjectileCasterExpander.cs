using System.Reflection;
using HarmonyLib;
using RR.Game.Character;
using UnityEngine;

namespace ThePit.FeralEngine.Abilities {
    // Shared logic for expanding a ProjectileCaster's layer masks to include the Player
    // layer so projectiles can hit champions. The four ProjectileCaster-based ability
    // patches all use the same fields; this class holds them once.
    internal static class ProjectileCasterExpander {
        internal static readonly FieldInfo LayerMaskField = AccessTools.Field(typeof(ProjectileCaster), "_layerMask");
        internal static readonly FieldInfo LayerMaskCharsField = AccessTools.Field(typeof(ProjectileCaster), "_layerMaskCharacters");
        internal static readonly FieldInfo LayerMaskDmgField = AccessTools.Field(typeof(ProjectileCaster), "_layerMaskDamage");
        internal static readonly FieldInfo ExcludeCasterField = AccessTools.Field(typeof(ProjectileCaster), "_excludeCasterLayer");

        internal static bool IsReady => LayerMaskField != null;

        internal readonly struct SavedMasks {
            internal readonly LayerMask Mask, MaskChars, MaskDmg;
            internal readonly bool ExcludeCaster;
            internal SavedMasks(LayerMask m, LayerMask mc, LayerMask md, bool ec) {
                Mask = m; MaskChars = mc; MaskDmg = md; ExcludeCaster = ec;
            }
        }

        // Expands a single caster, returns state needed to restore it.
        internal static SavedMasks Expand(ProjectileCaster caster) {
            int player = LayerMask.GetMask("Player");
            var saved = new SavedMasks(
                LayerMaskField != null ? (LayerMask)LayerMaskField.GetValue(caster) : default,
                LayerMaskCharsField != null ? (LayerMask)LayerMaskCharsField.GetValue(caster) : default,
                LayerMaskDmgField != null ? (LayerMask)LayerMaskDmgField.GetValue(caster) : default,
                ExcludeCasterField != null && (bool)ExcludeCasterField.GetValue(caster)
            );
            LayerMaskField?.SetValue(caster, (LayerMask)(saved.Mask.value | player));
            LayerMaskCharsField?.SetValue(caster, (LayerMask)(saved.MaskChars.value | player));
            LayerMaskDmgField?.SetValue(caster, (LayerMask)(saved.MaskDmg.value | player));
            ExcludeCasterField?.SetValue(caster, false);
            return saved;
        }

        internal static void Reset(ProjectileCaster caster, SavedMasks saved) {
            if (caster == null) { return; }
            LayerMaskField?.SetValue(caster, saved.Mask);
            LayerMaskCharsField?.SetValue(caster, saved.MaskChars);
            LayerMaskDmgField?.SetValue(caster, saved.MaskDmg);
            ExcludeCasterField?.SetValue(caster, saved.ExcludeCaster);
        }
    }
}
