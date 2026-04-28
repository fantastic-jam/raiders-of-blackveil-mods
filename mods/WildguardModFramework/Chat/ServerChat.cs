using HarmonyLib;
using RR;
using RR.Input;
using RR.UI.Controls.Menu.Options;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine.UIElements;

namespace WildguardModFramework.Chat {
    internal static class ServerChat {
        private static readonly System.Collections.Generic.Dictionary<BaseHUDPage, ServerChatOverlay> _overlays = new();
        private static BaseHUDPage _activePage;

        private static ServerChatOverlay ActiveOverlay =>
            _activePage != null && _overlays.TryGetValue(_activePage, out var o) ? o : null;

        internal static bool IsEnabled { get; set; } = true;
        internal static bool IsOpen { get; private set; }
        private static int _closeFrame = -1;
        internal static bool SuppressOpen => UnityEngine.Time.frameCount == _closeFrame;

        internal static void Init(Harmony harmony) {
            ServerChatNetwork.Init();
            ServerChatHudPatch.Apply(harmony);
        }

        // ── HUD lifecycle ──────────────────────────────────────────────────

        internal static void OnHudInit(BaseHUDPage page) {
            var overlay = new ServerChatOverlay(page.RootElement);
            overlay.OnSendRequested += OnSendRequested;
            _overlays[page] = overlay;
        }

        internal static void SetActivePage(BaseHUDPage page) => _activePage = page;

        // ── State transitions ──────────────────────────────────────────────

        internal static void Open() {
            if (!IsEnabled || !WmfConfig.ChatEnabled || IsOpen || ActiveOverlay == null) { return; }
            IsOpen = true;
            var im = InputManager.Instance;
            if (im != null) {
                im.PlayerInputEnable = PlayerInputEnableScope.Disable;
                im.SetCursorToMenuMode(true);
            }
            ActiveOverlay.ShowInputRow();
        }

        internal static void Close() {
            if (!IsOpen) { return; }
            IsOpen = false;
            _closeFrame = UnityEngine.Time.frameCount;
            var im = InputManager.Instance;
            if (im != null) {
                im.PlayerInputEnable = PlayerInputEnableScope.Allow;
                im.SetCursorToMenuMode(false);
            }
            ActiveOverlay?.HideInputRow();
        }

        internal static void ForceClose(BaseHUDPage page) {
            if (_overlays.TryGetValue(page, out var overlay)) {
                IsOpen = false;
                var im = InputManager.Instance;
                if (im != null) {
                    im.PlayerInputEnable = PlayerInputEnableScope.Allow;
                    im.SetCursorToMenuMode(false);
                }
                overlay.ForceHide();
            }
            if (_activePage == page) { _activePage = null; }
        }

        // ── Message handling ───────────────────────────────────────────────

        internal static void ClearAll() {
            IsOpen = false;
            foreach (var overlay in _overlays.Values) { overlay.ClearLog(); }
        }

        internal static void ReceiveMessage(string sender, string text) {
            if (!WmfConfig.ChatEnabled) { return; }
            foreach (var overlay in _overlays.Values) { overlay.AppendMessage(sender, text); }
        }

        private static void OnSendRequested(string text) {
            text = text?.Trim() ?? "";
            Close();
            if (string.IsNullOrEmpty(text)) { return; }

            var localName = PlayerManager.Instance?.LocalPlayer?.UserName ?? "?";
            var runner = PlayerManager.Instance?.Runner;

            if (runner?.IsServer == true) {
                ReceiveMessage(localName, text);
                ServerChatNetwork.HostBroadcast(localName, text);
            } else {
                ServerChatNetwork.SendToHost(localName, text);
            }
        }

        // ── WMF settings panel ─────────────────────────────────────────────

        internal static void BuildSettingsPanel(VisualElement container, bool isInGameMenu) {
            var toggle = new OptionOnOffSwitch {
                LabelKey = "@Server Chat",
                OnLabelKey = "@On",
                OffLabelKey = "@Off",
                Value = WmfConfig.ChatEnabled,
            };
            toggle.OnValueChangedCallback = v => WmfConfig.ChatEnabled = v;
            container.Add(toggle);
        }
    }
}
