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

        internal static void Init() {
            _validateReleaseMethod = AccessTools.Method(typeof(BackendManager), "StartAsyncValidateGameRelease");
        }

        // No-op if already logged in; otherwise shows login screens and awaits completion.
        public static Task EnsureLoggedIn() {
            if (IsLoggedIn) {
                return Task.CompletedTask;
            }

            if (_loginTcs != null) {
                OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Deferred login already in progress.");
                return _loginTcs.Task;
            }
            OfflineModeMod.PublicLogger.LogInfo("OfflineMode: Starting deferred login — showing login page.");
            _loginTcs = new TaskCompletionSource<bool>();
            UIManager.Instance.ChangePage("MenuValidateReleaseLoginPage", TransitionAnimation.Fade, crossFade: false);
            _validateReleaseMethod?.Invoke(BackendManager.Instance, null);
            return _loginTcs.Task;
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
