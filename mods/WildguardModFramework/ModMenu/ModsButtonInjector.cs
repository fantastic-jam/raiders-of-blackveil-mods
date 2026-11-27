using System;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine.UIElements;
using WildguardModFramework.Interop;

namespace WildguardModFramework.ModMenu {
    /// <summary>
    /// Injects the "Mods" button into the main menu and owns the ModsMenuOverlay instance.
    /// Called from MenuStartPagePatch.CtorPostfix (once per MenuStartPage construction).
    /// </summary>
    internal static class ModsButtonInjector {
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

            _exitBtn = instance.RootElement.Q<ButtonMenuStart>("ExitButton");
            _modsBtn = new ButtonMenuStartNormal {
                // ParentPage gates clicks through PageBase.AllowClick (blocked during transitions / popups).
                ParentPage = instance.Cast<IAllowClick>(),
                OnClick = (Action<ButtonMenuStart>)(_ => {
                    _overlay ??= new ModsMenuOverlay(_menuPageRoot);
                    _overlay.Open();
                }),
                Enabled = true
            };

            var lbl = _modsBtn.Q<LocLabel>("Label");
            if (lbl != null) {
                lbl.SetLiteral("", customTransform: (Func<string, string>)(_ => WmfMod.t("label.mods")));
            }

            container.Insert(container.IndexOf(settingsBtn) + 1, _modsBtn);
        }

        /// <summary>
        /// Re-registers the Mods button after MenuStartPage.ResetCursor rebuilt the cursor
        /// (runs on every OnActivate / OnReady). Placed just before the Exit button to match the visual order.
        /// </summary>
        internal static void RegisterInCursor(MenuStartPage instance) {
            var cursor = instance._cursor;
            if (_modsBtn == null || cursor == null) { return; }
            // The Mods button sits in the main container, hidden while the Play sub-menu is shown.
            // 0 = main buttons (where the Mods button lives), 1 = Play sub-menu (since game 0.6.5).
            if (instance._buttonsPageIndex != 0) { return; }
            cursor.RegisterItem(_modsBtn.Cast<ICursorLinearStopElement>(), _modsBtn);

            var items = cursor._items;
            if (_exitBtn == null || items == null) { return; }
            var exitIdx = -1;
            for (var i = 0; i < items.Count; i++) {
                if (items[i].Element.SameAs(_exitBtn)) { exitIdx = i; break; }
            }
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
