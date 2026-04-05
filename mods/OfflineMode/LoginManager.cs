using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RR;
using RR.Backend;
using RR.UI.UISystem;
using RR.UI.Utils;

namespace OfflineMode {
    // Manages the deferred login flow — shows login screens on demand, no-ops if already logged in.
    internal static class LoginManager {
        private static TaskCompletionSource<bool> _loginTcs;
        private static MethodInfo _validateReleaseMethod;

        public static bool IsLoggedIn { get; private set; }
        private static bool _validated;

        internal static void Init() {
            _validateReleaseMethod = AccessTools.Method(typeof(BackendManager), "StartAsyncValidateGameRelease");
        }

        // Called when the user enters offline mode — next online attempt must re-validate saves.
        internal static void InvalidateValidation() => _validated = false;

        // Ensures the user is logged in and save state is validated.
        // - No-op if already logged in and validated.
        // - Validates only if logged in but not yet validated (e.g. after an offline session).
        // - Full login flow if not logged in: shows login page, awaits, navigates back to MenuStartPage.
        public static async Task EnsureLoggedIn() {
            if (IsLoggedIn && _validated) {
                return;
            }

            if (!IsLoggedIn) {
                if (_loginTcs != null) {
                    OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Deferred login already in progress — waiting.");
                    await _loginTcs.Task;
                } else {
                    OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Starting deferred login — showing login page.");
                    _loginTcs = new TaskCompletionSource<bool>();
                    UIManager.Instance.ChangePage("MenuValidateReleaseLoginPage", TransitionAnimation.Fade, crossFade: false);
                    _validateReleaseMethod?.Invoke(BackendManager.Instance, null);
                    await _loginTcs.Task;
                }
            }

            // ValidatePlayerGameState calls ClosePage() internally — mirrors the game's own flow
            // (AppManager: validate → ChangePage MenuStartPage). Always validate before navigating.
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Validating save state.");
            await PlayerManager.ValidatePlayerGameState();
            _validated = true;
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Save validation complete — navigating to MenuStartPage.");
            UIManager.Instance.ChangePage("MenuStartPage", TransitionAnimation.Fade, crossFade: false);
        }

        // Called by the StartAsyncValidateGameRelease patch.
        // Blocks the startup call from BackendManager.Init(); lets user-triggered calls through.
        private static bool _validationDeferred;
        internal static bool ShouldAllowValidateRelease() {
            if (!_validationDeferred) { _validationDeferred = true; return false; }
            return true;
        }

        // Called by the DisclaimerManager ctor patch — fires right after login completes.
        // Returns false (skips ctor) when resolving a pending deferred login.
        // Returns the best available WomboPlayer: real if logged in, saved if offline.
        public static WomboPlayer GetWomboPlayer() =>
            IsLoggedIn ? BackendManager.Instance?.LoggedInWomboPlayer : OfflineSaveManager.LoadWomboPlayer();

        internal static bool OnDisclaimerManagerCreated() {
            OfflineModeMod.PublicLogger.LogInfo($"OfflineMode: LoginManager.OnDisclaimerManagerCreated — pending={_loginTcs != null}.");
            if (_loginTcs == null) {
                return true;
            }

            IsLoggedIn = true;
            OfflineSaveManager.SaveWomboPlayer(BackendManager.Instance?.LoggedInWomboPlayer);
            _loginTcs.TrySetResult(true);
            _loginTcs = null;
            return false;
        }
    }
}
