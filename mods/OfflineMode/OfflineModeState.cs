namespace OfflineMode {
    public static class OfflineModeState {
        private static bool _isOffline;
        public static bool IsOffline {
            get => _isOffline;
            internal set {
                _isOffline = value;
                if (value) {
                    LoginManager.InvalidateValidation();
                }
            }
        }
    }
}
