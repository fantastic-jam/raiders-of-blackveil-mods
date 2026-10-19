using System.Reflection;
using HarmonyLib;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    internal static class MenuPausePagePatch {
        private static FieldInfo _cursorField;
        private static ModsMenuOverlay _overlay;
        private static ButtonShared _modsButton;
        private static VisualElement _pageRoot;

        internal static void Apply(Harmony harmony) {
            _cursorField = AccessTools.Field(typeof(MenuPausePage), "_cursor");
            if (_cursorField == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuPausePage._cursor not found — Mods button keyboard nav unavailable.");
            }

            // Pages no longer have an OnInit hook — all init happens in the PageArgs constructor.
            var ctor = AccessTools.Constructor(typeof(MenuPausePage), new[] { typeof(PageArgs) });
            if (ctor == null) {
                WmfMod.PublicLogger.LogWarning("WMF: MenuPausePage(PageArgs) constructor not found — in-game Mods button skipped.");
                return;
            }
            harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(MenuPausePagePatch), nameof(CtorPostfix)));

            var onActivate = AccessTools.Method(typeof(MenuPausePage), "OnActivate");
            if (onActivate != null) {
                harmony.Patch(onActivate, postfix: new HarmonyMethod(typeof(MenuPausePagePatch), nameof(OnActivatePostfix)));
            }

            var onNavigate = AccessTools.Method(typeof(MenuPausePage), "OnNavigateInput");
            if (onNavigate != null) {
                harmony.Patch(onNavigate, prefix: new HarmonyMethod(typeof(MenuPausePagePatch), nameof(OnNavigateInputPrefix)));
            }
        }

        private static void CtorPostfix(MenuPausePage __instance) {
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
                ParentPage = __instance,
                IsLarge = optionsBtn.IsLarge,
                SoundType = ButtonSoundType.PauseMenu,
                OnClicked = OpenOverlay
            };
            _modsButton.style.alignSelf = Align.Stretch;

            // ButtonShared.LabelCustomTransform was removed — set the text on the inner LocLabel directly.
            var lbl = _modsButton.Q<LocLabel>("Label");
            if (lbl != null) {
                lbl.SetLiteral("", customTransform: _ => WmfMod.t("label.mods"));
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
            var cursor = _cursorField?.GetValue(__instance) as UICursorLinear<object>;
            cursor?.RegisterItem(_modsButton);
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
