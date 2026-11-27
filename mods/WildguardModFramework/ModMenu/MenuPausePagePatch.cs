using System;
using HarmonyLib;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine.UIElements;
using WildguardModFramework.Interop;

namespace WildguardModFramework.ModMenu {
    internal static class MenuPausePagePatch {
        private static ModsMenuOverlay _overlay;
        private static ButtonShared _modsButton;
        private static VisualElement _pageRoot;
        private static readonly InstanceTracker _page = new();

        internal static void Apply(Harmony harmony) {
            // Pages are built in their PageArgs constructor, but IL2CPP constructors can't be patched:
            // inject the button on the first OnActivate of each page instance (prefix), register it after (postfix).
            var onActivate = AccessTools.Method(typeof(MenuPausePage), "OnActivate");
            if (onActivate == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuPausePage.OnActivate not found — in-game Mods button skipped.");
                return;
            }
            harmony.Patch(onActivate,
                prefix: new HarmonyMethod(typeof(MenuPausePagePatch), nameof(OnActivatePrefix)),
                postfix: new HarmonyMethod(typeof(MenuPausePagePatch), nameof(OnActivatePostfix)));

            var onNavigate = AccessTools.Method(typeof(MenuPausePage), "OnNavigateInput");
            if (onNavigate != null) {
                harmony.Patch(onNavigate, prefix: new HarmonyMethod(typeof(MenuPausePagePatch), nameof(OnNavigateInputPrefix)));
            }
        }

        private static void OnActivatePrefix(MenuPausePage __instance) => EnsureInjected(__instance);

        private static void EnsureInjected(MenuPausePage page) {
            if (_page.IsNew(page)) { Inject(page); }
        }

        private static void Inject(MenuPausePage __instance) {
            _pageRoot = __instance.RootElement;
            _overlay = null;
            _modsButton = null;

            var optionsBtn = _pageRoot.Q<ButtonShared>("OptionsButton");
            if (optionsBtn == null) {
                WmfMod.PublicLogger.LogWarning("WMF: OptionsButton not found in pause menu — Mods button skipped.");
                return;
            }

            var container = optionsBtn.parent;
            if (container == null) { return; }

            _modsButton = new ButtonShared {
                // ParentPage gates clicks through PageBase.AllowClick (blocked during transitions / popups).
                ParentPage = __instance.Cast<IAllowClick>(),
                IsLarge = optionsBtn.IsLarge,
                SoundType = ButtonSoundType.PauseMenu,
                OnClicked = (Action)OpenOverlay
            };
            _modsButton.style.alignSelf = Align.Stretch;

            // ButtonShared.LabelCustomTransform was removed — set the text on the inner LocLabel directly.
            var lbl = _modsButton.Q<LocLabel>("Label");
            if (lbl != null) {
                lbl.SetLiteral("", customTransform: (Func<string, string>)(_ => WmfMod.t("label.mods")));
            }

            container.Insert(container.IndexOf(optionsBtn) + 1, _modsButton);
        }

        // Overlay is created lazily on first open — by then Scan() is guaranteed to have run.
        private static void OpenOverlay() {
            _overlay ??= new ModsMenuOverlay(_pageRoot, isInGameMenu: true);
            _overlay.Open();
        }

        private static void OnActivatePostfix(MenuPausePage __instance) {
            if (_modsButton == null) { return; }
            __instance._cursor?.RegisterItem(_modsButton.Cast<ICursorLinearStopElement>());
        }

        private static bool OnNavigateInputPrefix(InputPressEvent evt) {
            if (_overlay is { IsOpen: true }) {
                _overlay.OnNavigateInput(evt);
                return false;
            }
            return true;
        }
    }
}
