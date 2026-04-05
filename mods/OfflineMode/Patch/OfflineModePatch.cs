using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RR;
using RR.UI.Components;
using RR.UI.Controls;
using RR.UI.Controls.Menu;
using RR.UI.Pages;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine.UIElements;

namespace OfflineMode.Patch {
    public static class OfflineModePatch {
        private static TaskCompletionSource<bool> _loginTcs;
        private static bool _startupBypassed;
        private static MethodInfo _startApplicationMethod;
        private static MethodInfo _validateReleaseMethod;
        private static FieldInfo _cursorField;
        private static MethodInfo _startClickMethod;

        public static void Apply(Harmony harmony) {
            void Patch(MethodBase method, string warning, HarmonyMethod prefix = null, HarmonyMethod postfix = null) {
                if (method == null) { OfflineModeMod.PublicLogger.LogWarning(warning); return; }
                harmony.Patch(method, prefix: prefix, postfix: postfix);
            }

            _startApplicationMethod = AccessTools.Method(typeof(SettingsHandler), "StartApplication");
            if (_startApplicationMethod == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: Could not find SettingsHandler.StartApplication.");
            }

            _validateReleaseMethod = AccessTools.Method(typeof(BackendManager), "StartAsyncValidateGameRelease");

            _cursorField = AccessTools.Field(typeof(MenuStartPage), "_cursor");
            if (_cursorField == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: Could not find MenuStartPage._cursor — keyboard navigation for Offline Mode button unavailable.");
            }

            _startClickMethod = AccessTools.Method(typeof(MenuStartPage), "StartClick",
                new[] { typeof(BackendManager.PlaySessionMode), typeof(bool), typeof(string), typeof(string) });
            if (_startClickMethod == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: Could not find MenuStartPage.StartClick — Offline Mode button inactive.");
            }

            Patch(
                AccessTools.Method(typeof(SettingsHandler), "Handle_AppEvent_Initialized"),
                "OfflineMode: Could not find SettingsHandler.Handle_AppEvent_Initialized.",
                prefix: new HarmonyMethod(typeof(OfflineModePatch), nameof(HandleAppEventInitializedPrefix)));

            Patch(
                AccessTools.Method(typeof(MenuStartPage), "OnInit"),
                "OfflineMode: Could not find MenuStartPage.OnInit — button injection skipped.",
                postfix: new HarmonyMethod(typeof(OfflineModePatch), nameof(MenuStartPageOnInitPostfix)));

            // Intercept DisclaimerManager constructor — fires right after login completes.
            // During deferred login: skip screens, resolve promise, caller unhides the menu.
            // During startup bypass: screens run normally; StartMainMenuOrTutorial is intercepted.
            Patch(
                AccessTools.Constructor(typeof(DisclaimerManager)),
                "OfflineMode: Could not find DisclaimerManager constructor — deferred login may not resolve.",
                prefix: new HarmonyMethod(typeof(OfflineModePatch), nameof(DisclaimerManagerCtorPrefix)));

            Patch(
                AccessTools.Method(typeof(AppManager), "StartMainMenuOrTutorial"),
                "OfflineMode: Could not find AppManager.StartMainMenuOrTutorial — startup disclaimer flow may reload scene.",
                prefix: new HarmonyMethod(typeof(OfflineModePatch), nameof(StartMainMenuOrTutorialPrefix)));

            OfflineBackendPatch.Apply(harmony);

            OfflineModeMod.PublicLogger.LogInfo("OfflineMode patch applied.");
        }

        // On startup: skip login page and show disclaimer screens directly.
        // After the screens the game calls StartMainMenuOrTutorial — we intercept that to go
        // straight to MenuStartPage instead of reloading the scene.
        // Exception: first launch (AppStartCount < 2) must use the normal flow for the tutorial.
        private static bool HandleAppEventInitializedPrefix(SettingsHandler __instance) {
            if (AppManager.Instance.PlayerSettings.Game_AppStartCount < 2) {
                OfflineModeMod.PublicLogger.LogInfo("OfflineMode: First launch — letting normal login flow run for tutorial.");
                return true;
            }
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Startup — skipping login page, showing disclaimers.");
            _startApplicationMethod?.Invoke(__instance, null);
            _startupBypassed = true;
            _ = new DisclaimerManager();
            return false;
        }

        // After disclaimer screens close, the game calls StartMainMenuOrTutorial.
        // During startup bypass: just go to MenuStartPage — no scene reload needed.
        private static bool StartMainMenuOrTutorialPrefix() {
            if (!_startupBypassed) { return true; }
            _startupBypassed = false;
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Disclaimers done — navigating to MenuStartPage.");
            UIManager.Instance.ChangePage("MenuStartPage", TransitionAnimation.Fade, crossFade: false);
            return false;
        }

        // Fires right after login completes (game creates DisclaimerManager at that point).
        // If a deferred login is pending: resolve the promise and skip the disclaimer screens —
        // we are already on MenuStartPage, the caller will just unhide it.
        private static bool DisclaimerManagerCtorPrefix() {
            if (_loginTcs == null) { return true; } // normal startup flow — run as usual
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Login complete (DisclaimerManager intercepted) — resolving login promise.");
            _loginTcs.TrySetResult(true);
            _loginTcs = null;
            return false; // skip disclaimer screens and StartMainMenuOrTutorial
        }

        private static Task<bool> DeferredLoginAndValidation() {
            if (_loginTcs != null) {
                OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Deferred login already in progress.");
                return _loginTcs.Task;
            }
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Starting deferred login — showing login page.");
            _loginTcs = new TaskCompletionSource<bool>();
            // Show the login page — it handles validation events and calls login on its own.
            // DisclaimerManagerCtorPrefix will intercept once login is complete.
            UIManager.Instance.ChangePage("MenuValidateReleaseLoginPage", TransitionAnimation.Fade, crossFade: false);
            _validateReleaseMethod?.Invoke(BackendManager.Instance, null);
            return _loginTcs.Task;
        }

        private static void MenuStartPageOnInitPostfix(MenuStartPage __instance) {
            // Inject the Offline Mode button above Exit.
            var exitBtn = __instance.RootElement.Q<VisualElement>("ExitButton");
            if (exitBtn == null) {
                OfflineModeMod.PublicLogger.LogWarning("OfflineMode: ExitButton not found — Offline Mode button skipped.");
            } else {
                var container = exitBtn.parent;
                if (container != null) {
                    var hasSave = OfflineSaveManager.HasAnySave();
                    var offlineBtn = new ButtonGeneric3 {
                        OnClick = _ => OnOfflineModeClicked(__instance),
                        Enabled = hasSave
                    };

                    var cursor = (UICursorLinear<object>)_cursorField?.GetValue(__instance);
                    cursor?.RegisterItem(offlineBtn);

                    var lbl = offlineBtn.Q<LocLabel>("Label");
                    if (lbl != null) {
                        lbl.CustomTransform = _ => "Offline Mode";
                        lbl.Refresh();
                    }

                    container.Insert(container.IndexOf(exitBtn), offlineBtn);
                }
            }

            // Wrap play buttons: if not logged in, run the silent login flow first.
            foreach (var btnName in new[] {
                "NewSinglePlayerGameButton",
                "TutorialButton",
                "JoinMultiPlayerGameButton",
                "HostMultiPlayerGameButton"
            }) {
                var playBtn = __instance.RootElement.Q<ButtonGeneric3>(btnName);
                if (playBtn == null) { continue; }

                var original = playBtn.OnClick;
                playBtn.OnClick = async btn => {
                    if (OfflineModeState.IsOffline || OfflineModeState.IsLoggedIn()) {
                        original?.Invoke(btn);
                        return;
                    }
                    OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: '{btnName}' clicked — login required, starting silent login.");
                    try {
                        __instance.RootElement.style.display = DisplayStyle.None;
                        await DeferredLoginAndValidation();
                        // Run the game's built-in local vs backend save conflict check,
                        // which may show a popup letting the user pick which save to keep.
                        await PlayerManager.ValidatePlayerGameState();
                        OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: Login and save validation done — unhiding menu and submitting '{btnName}'.");
                        __instance.RootElement.style.display = DisplayStyle.Flex;
                        __instance.RootElement.Q<ButtonGeneric3>(btnName)?.Submit();
                    }
                    catch (Exception ex) {
                        OfflineModeMod.PublicLogger.LogError($"OfflineMode: Deferred login failed — {ex.Message}");
                        __instance.RootElement.style.display = DisplayStyle.Flex;
                    }
                };
            }
        }

        private static void OnOfflineModeClicked(MenuStartPage page) {
            OfflineModeState.IsOffline = true;
            _startClickMethod?.Invoke(page, new object[] {
                BackendManager.PlaySessionMode.SinglePlayer, false, "", ""
            });
        }
    }
}
