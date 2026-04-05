using System;
using System.Reflection;
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
        private static bool _startupBypassed;
        private static MethodInfo _startApplicationMethod;
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

            LoginManager.Init();

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
            // During deferred login: LoginManager resolves the promise and skips disclaimer screens.
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
        private static bool DisclaimerManagerCtorPrefix() => LoginManager.OnDisclaimerManagerCreated();

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

            // Wrap play buttons: clear IsOffline, trigger login if not yet logged in.
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
                    OfflineModeState.IsOffline = false;
                    OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: '{btnName}' clicked.");
                    try {
                        await LoginManager.EnsureLoggedIn();
                        OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: Ready — invoking '{btnName}'.");
                        original?.Invoke(btn);
                    }
                    catch (Exception ex) {
                        OfflineModeMod.PublicLogger.LogError($"OfflineMode: Login failed — {ex.Message}");
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
