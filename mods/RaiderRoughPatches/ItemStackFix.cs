using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Items;
using RR.UI.Controls.Inventory;
using RR.UI.Pages;

namespace RaiderRoughPatches {
    internal static class ItemStackFix {
        private static FieldInfo _otherPanelField;
        private static FieldInfo _championPanelField;

        internal static void Init() {
            _otherPanelField = AccessTools.Field(typeof(GameInventoryPage), "_otherPanel");
            if (_otherPanelField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: GameInventoryPage._otherPanel not found — stash auto-stack inactive.");
            }

            _championPanelField = AccessTools.Field(typeof(GameInventoryPage), "_championPanel");
            if (_championPanelField == null) {
                RaiderRoughPatchesMod.PublicLogger.LogWarning(
                    "RaiderRoughPatches: GameInventoryPage._championPanel not found — stash auto-stack inactive.");
            }
        }

        internal static bool IsReady => _otherPanelField != null && _championPanelField != null;

        // Postfix for CanMergeItem: vanilla only checks ItemType, so any two Souvenirs appear mergeable.
        // Adding AssetID equality prevents e.g. a hex doll merging into a pig stew slot.
        internal static void FixCanMergeItem(InventorySlotNormal __instance, InventoryItem sourceItem, ref bool __result) {
            if (!__result) { return; }
            if (__instance.Item?.Descriptor?.AsGenericItem?.AssetID != sourceItem?.Descriptor?.AsGenericItem?.AssetID) {
                __result = false;
            }
        }

        internal static bool TryAutoStack(GameInventoryPage instance, InventorySlot sourceSlot) {
            if (!(sourceSlot is InventorySlotNormal source) || source.IsEmpty) {
                return true;
            }

            if (source.Item?.Descriptor?.IsStackable != true) {
                return true;
            }

            InventoryPanel targetPanel = source.GridTab.IsChampion()
                ? _otherPanelField?.GetValue(instance) as InventoryPanel
                : _championPanelField?.GetValue(instance) as InventoryPanel;

            if (targetPanel == null) {
                return true;
            }

            var slots = new List<InventorySlot>();
            targetPanel.AppendAllSlotsToList(slots);

            foreach (var slot in slots) {
                if (!(slot is InventorySlotNormal target) || !target.CanMergeItem(source.Item)) {
                    continue;
                }

                var targetDesc = target.Item.Descriptor;
                var sourceDesc = source.Item.Descriptor;
                int space = targetDesc.AmountMaximum - targetDesc.Amount;

                if (space >= sourceDesc.Amount) {
                    targetDesc.Amount += sourceDesc.Amount;
                    sourceDesc.Amount = 0;
                    target.Item.RefreshStack();
                    source.DetachItem(deselectSlot: true);
                    return false;
                }

                targetDesc.Amount = targetDesc.AmountMaximum;
                sourceDesc.Amount -= space;
                target.Item.RefreshStack();
                source.Item.RefreshStack();
            }

            return true;
        }

    }
}
