using System.Reflection;
using HarmonyLib;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.Utils;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Injects the "Mods" button into the main menu and owns the ModsMenuOverlay instance.
    /// Called once from MenuStartPagePatch.OnInitPostfix.
    /// </summary>
    internal static class ModsButtonInjector {
        private static readonly FieldInfo CursorField = AccessTools.Field(typeof(MenuStartPage), "_cursor");

        private static ModsMenuOverlay _overlay;
        private static VisualElement _menuPageRoot;

        internal static void Inject(MenuStartPage instance) {
            _menuPageRoot = instance.RootElement;

            var settingsBtn = instance.RootElement.Q<VisualElement>("SettingsButton");
            if (settingsBtn == null) {
                WmfMod.PublicLogger.LogWarning("WMF: SettingsButton not found — Mods button skipped.");
                return;
            }

            var container = settingsBtn.parent;
            if (container == null) { return; }

            var cursor = CursorField?.GetValue(instance) as UICursorLinear<object>;

            var modsBtn = new ButtonGeneric3 {
                OnClick = _ => {
                    _overlay ??= new ModsMenuOverlay(_menuPageRoot);
                    _overlay.Open();
                },
                Enabled = true
            };
            cursor?.RegisterItem(modsBtn);

            var lbl = modsBtn.Q<LocLabel>("Label");
            if (lbl != null) {
                lbl.CustomTransform = _ => "Mods";
                lbl.Refresh();
            }

            container.Insert(container.IndexOf(settingsBtn) + 1, modsBtn);
        }

        /// <summary>Returns false if the overlay consumed the input (skip original handler).</summary>
        internal static bool HandleInput(InputPressEvent evt) {
            if (_overlay is not { IsOpen: true }) { return false; }
            _overlay.OnNavigateInput(evt);
            return true;
        }
    }
}
