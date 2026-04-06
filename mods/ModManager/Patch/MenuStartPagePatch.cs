using System.Reflection;
using HarmonyLib;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.Utils;
using UnityEngine.UIElements;

namespace ModManager.Patch {
    internal static class MenuStartPagePatch {
        private static FieldInfo _cursorField;
        private static ModsMenuOverlay _overlay;

        internal static void Apply(Harmony harmony) {
            _cursorField = AccessTools.Field(typeof(MenuStartPage), "_cursor");
            if (_cursorField == null) {
                ModManagerMod.PublicLogger.LogWarning("ModManager: MenuStartPage._cursor not found — Mods button keyboard nav unavailable.");
            }

            var onInit = AccessTools.Method(typeof(MenuStartPage), "OnInit");
            if (onInit == null) {
                ModManagerMod.PublicLogger.LogWarning("ModManager: MenuStartPage.OnInit not found — Mods button skipped.");
                return;
            }
            harmony.Patch(onInit, postfix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(OnInitPostfix)));

            var onNavigate = AccessTools.Method(typeof(MenuStartPage), "OnNavigateInput");
            if (onNavigate != null) {
                harmony.Patch(onNavigate, prefix: new HarmonyMethod(typeof(MenuStartPagePatch), nameof(OnNavigateInputPrefix)));
            }
        }

        private static void OnInitPostfix(MenuStartPage __instance) {
            // Scan here — all BepInEx plugins are guaranteed to be loaded by the time
            // the main menu initializes, so every plugin's Instance is non-null.
            ModManagerRegistrants.Scan();
            ModManagerConfig.Sync(ModManagerRegistrants.AllMods());
            ModManagerRegistrants.ApplyStartupDisables();

            // Create the overlay (built once, shown/hidden on demand)
            _overlay = new ModsMenuOverlay(__instance.RootElement);

            // Inject "Mods" button after SettingsButton
            var settingsBtn = __instance.RootElement.Q<VisualElement>("SettingsButton");
            if (settingsBtn == null) {
                ModManagerMod.PublicLogger.LogWarning("ModManager: SettingsButton not found — Mods button skipped.");
                return;
            }

            var container = settingsBtn.parent;
            if (container == null) { return; }

            var cursor = _cursorField?.GetValue(__instance) as UICursorLinear<object>;

            var modsBtn = new ButtonGeneric3 { OnClick = _ => _overlay?.Open(), Enabled = true };
            cursor?.RegisterItem(modsBtn);

            var lbl = modsBtn.Q<LocLabel>("Label");
            if (lbl != null) {
                lbl.CustomTransform = _ => "Mods";
                lbl.Refresh();
            }

            container.Insert(container.IndexOf(settingsBtn) + 1, modsBtn);
        }

        // Prefix: when the overlay is open, forward all input to it and skip the main menu cursor.
        private static bool OnNavigateInputPrefix(InputPressEvent evt) {
            if (_overlay is { IsOpen: true }) {
                _overlay.OnNavigateInput(evt);
                return false; // skip original OnNavigateInput
            }
            return true;
        }
    }
}
