using System.Collections.Generic;
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
    /// Called from MenuStartPagePatch.CtorPostfix (once per MenuStartPage construction).
    /// </summary>
    internal static class ModsButtonInjector {
        private static readonly FieldInfo CursorField = AccessTools.Field(typeof(MenuStartPage), "_cursor");
        private static readonly FieldInfo CursorItemsField = AccessTools.Field(typeof(UICursorLinear<ButtonMenuStart>), "_items");

        private static ModsMenuOverlay _overlay;
        private static VisualElement _menuPageRoot;
        private static ButtonMenuStart _modsBtn;
        private static ButtonMenuStart _exitBtn;

        internal static void Inject(MenuStartPage instance) {
            _menuPageRoot = instance.RootElement;
            _overlay = null;
            _modsBtn = null;

            var settingsBtn = instance.RootElement.Q<VisualElement>("SettingsButton");
            if (settingsBtn == null) {
                WmfMod.PublicLogger.LogWarning("WMF: SettingsButton not found — Mods button skipped.");
                return;
            }

            var container = settingsBtn.parent;
            if (container == null) { return; }

            if (CursorField == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuStartPage._cursor not found — Mods button keyboard/gamepad nav unavailable.");
            }
            if (CursorItemsField == null) {
                WmfMod.PublicLogger.LogWarning("WMF: UICursorLinear._items not found — Mods button will be last in keyboard/gamepad nav order.");
            }

            _exitBtn = instance.RootElement.Q<ButtonMenuStart>("ExitButton");
            _modsBtn = new ButtonMenuStartNormal {
                // ParentPage gates clicks through PageBase.AllowClick (blocked during transitions / popups).
                ParentPage = instance,
                OnClick = _ => {
                    _overlay ??= new ModsMenuOverlay(_menuPageRoot);
                    _overlay.Open();
                },
                Enabled = true
            };

            var lbl = _modsBtn.Q<LocLabel>("Label");
            if (lbl != null) {
                lbl.SetLiteral("", customTransform: _ => WmfMod.t("label.mods"));
            }

            container.Insert(container.IndexOf(settingsBtn) + 1, _modsBtn);
        }

        /// <summary>
        /// Re-registers the Mods button after MenuStartPage.ResetCursor rebuilt the cursor
        /// (runs on every OnActivate / OnReady). Placed just before the Exit button to match the visual order.
        /// </summary>
        internal static void RegisterInCursor(MenuStartPage instance) {
            if (_modsBtn == null || CursorField?.GetValue(instance) is not UICursorLinear<ButtonMenuStart> cursor) { return; }
            cursor.RegisterItem(_modsBtn, _modsBtn);

            if (_exitBtn == null || CursorItemsField?.GetValue(cursor) is not List<UICursorLinear<ButtonMenuStart>.CursorStopItem> items) { return; }
            var exitIdx = items.FindIndex(i => i.Element == _exitBtn);
            if (exitIdx < 0) { return; }
            var modsItem = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            items.Insert(exitIdx, modsItem);
        }

        /// <summary>
        /// Invalidates the overlay so it is recreated on next open.
        /// Call when scan data changes (e.g. after a language change).
        /// </summary>
        internal static void Reset() => _overlay = null;

        /// <summary>Returns false if the overlay consumed the input (skip original handler).</summary>
        internal static bool HandleInput(InputPressEvent evt) {
            if (_overlay is not { IsOpen: true }) { return false; }
            _overlay.OnNavigateInput(evt);
            return true;
        }
    }
}
