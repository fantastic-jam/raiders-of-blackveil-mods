using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RR.Game.Items;
using RR.UI.Controls.Inventory;
using RR.UI.Pages;

namespace RaiderRoughPatches {
    internal static class StashStacker {
        internal static readonly FieldInfo OtherPanelField = AccessTools.Field(typeof(GameInventoryPage), "_otherPanel");

        internal static bool TryAutoStack(GameInventoryPage instance, InventorySlot sourceSlot) {
            if (!(sourceSlot is InventorySlotNormal source) || source.IsEmpty) {
                return true;
            }

            if (source.Item?.Descriptor?.IsStackable != true) {
                return true;
            }

            if (OtherPanelField == null) {
                return true;
            }

            if (!source.GridTab.IsChampion()) {
                return true;
            }

            if (!(OtherPanelField.GetValue(instance) is InventoryStashPanel targetPanel)) {
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
