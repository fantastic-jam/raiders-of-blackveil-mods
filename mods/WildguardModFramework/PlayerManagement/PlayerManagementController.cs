using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Fusion;
using HarmonyLib;
using RR;
using RR.Input;
using RR.UI.Pages;
using RR.UI.UISystem;
using UnityEngine;
using UnityEngine.UIElements;
using WildguardModFramework.Network;

namespace WildguardModFramework.PlayerManagement {
    internal static class PlayerManagementController {
        private static readonly Dictionary<BaseHUDPage, PlayerManagementOverlay> _overlays = new();
        private static BaseHUDPage _activePage;

        private static PlayerManagementOverlay ActiveOverlay =>
            _activePage != null && _overlays.TryGetValue(_activePage, out var o) ? o : null;

        internal static void Init(ConfigFile cfg, Harmony harmony) {
            BanList.Init(cfg);
            PlayerManagementPatch.Apply(harmony);
        }

        // ── F2 overlay ────────────────────────────────────────────────────

        internal static void SetActivePage(BaseHUDPage page) => _activePage = page;

        internal static void ShowOverlay() {
            var overlay = ActiveOverlay;
            if (overlay == null || overlay.IsVisible) { return; }
            var im = InputManager.Instance;
            if (im != null) {
                im.PlayerInputEnable = PlayerInputEnableScope.Disable;
                im.SetCursorToMenuMode(true);
            }
            overlay.Show(BuildRows(), PlayerManager.Instance?.Runner?.IsServer == true);
        }

        internal static void HideOverlay() => Close();

        private static bool _pendingRefresh;

        internal static void MarkDirty() => _pendingRefresh = true;

        internal static void TickInputMode() {
            if (ActiveOverlay?.IsVisible != true) { _pendingRefresh = false; return; }
            var im = InputManager.Instance;
            if (im != null) {
                im.PlayerInputEnable = PlayerInputEnableScope.Disable;
                im.SetCursorToMenuMode(true);
            }
            if (_pendingRefresh) { _pendingRefresh = false; RefreshOverlay(); }
        }

        internal static void RefreshOverlay() {
            var overlay = ActiveOverlay;
            if (overlay == null || !overlay.IsVisible) { return; }
            overlay.Show(BuildRows(), PlayerManager.Instance?.Runner?.IsServer == true);
        }

        private static List<PlayerRow> BuildRows() {
            var runner = PlayerManager.Instance?.Runner;
            var pm = PlayerManager.Instance;
            var rows = new List<PlayerRow>();
            if (runner == null || pm == null) { return rows; }
            foreach (var playerRef in runner.ActivePlayers) {
                var player = pm.GetPlayer(playerRef);
                if (player == null) { continue; }
                bool isModded = GameModeProtocol.IsKnownModded(playerRef);
                rows.Add(new PlayerRow(playerRef, player.ProfileUUID, player.UserName ?? "?", isModded, player.IsLocal));
            }
            return rows;
        }

        internal static void ForceClose(BaseHUDPage page) {
            if (_overlays.TryGetValue(page, out var overlay) && overlay.IsVisible) {
                overlay.Hide();
                var im = InputManager.Instance;
                if (im != null) {
                    im.PlayerInputEnable = PlayerInputEnableScope.Allow;
                    im.SetCursorToMenuMode(false);
                }
            }
            if (_activePage == page) { _activePage = null; }
        }

        private static void Close() {
            ActiveOverlay?.Hide();
            var im = InputManager.Instance;
            if (im != null) {
                im.PlayerInputEnable = PlayerInputEnableScope.Allow;
                im.SetCursorToMenuMode(false);
            }
        }

        // ── HUD lifecycle ──────────────────────────────────────────────────

        internal static void OnHudInit(BaseHUDPage page) {
            _overlays[page] = new PlayerManagementOverlay(page.RootElement, Close);
        }

        // ── Network events ─────────────────────────────────────────────────

        internal static void OnSetUserData(PlayerManager pm, PlayerRef playerRef, Guid profileUuid) {
            WmfMod.PublicLogger.LogInfo($"WMF: SetUserData({playerRef.PlayerId}) isServer={pm.Runner?.IsServer} banned={BanList.IsBanned(profileUuid)}.");
            if (pm.Runner?.IsServer != true || !BanList.IsBanned(profileUuid)) { return; }
            WmfMod.PublicLogger.LogInfo($"WMF: {playerRef.PlayerId} is banned — disconnecting.");
            pm.StartCoroutine(GameModeProtocol.DisconnectAfterDelayCoroutine(pm.Runner, playerRef));
        }

        // ── Ban list menu panel ────────────────────────────────────────────

        internal static void BuildBanListPanel(VisualElement container, bool isInGameMenu) {
            var header = new Label { text = WmfMod.t("players.banned.header") };
            header.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            header.style.fontSize = 11;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.letterSpacing = 1f;
            header.style.marginBottom = 10;
            header.pickingMode = PickingMode.Ignore;
            container.Add(header);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            container.Add(scroll);

            RefreshBanList(scroll);
        }

        private static void RefreshBanList(ScrollView scroll) {
            scroll.Clear();
            var entries = BanList.All();

            if (entries.Count == 0) {
                var empty = new Label { text = WmfMod.t("players.banned.empty") };
                empty.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                empty.style.fontSize = 12;
                empty.pickingMode = PickingMode.Ignore;
                scroll.Add(empty);
                return;
            }

            foreach (var (id, displayName) in entries) {
                var capturedId = id;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.marginBottom = 4;

                var nameLabel = new Label { text = displayName };
                nameLabel.style.color = Color.white;
                nameLabel.style.fontSize = 12;
                nameLabel.style.flexGrow = 1;
                nameLabel.pickingMode = PickingMode.Ignore;
                row.Add(nameLabel);

                var unbanBtn = new Button(() => {
                    BanList.Remove(capturedId);
                    RefreshBanList(scroll);
                }) { text = WmfMod.t("players.banned.unban") };
                unbanBtn.style.fontSize = 11;
                unbanBtn.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
                unbanBtn.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                unbanBtn.style.borderTopColor = unbanBtn.style.borderRightColor =
                    unbanBtn.style.borderBottomColor = unbanBtn.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 1f);
                unbanBtn.style.borderTopWidth = unbanBtn.style.borderRightWidth =
                    unbanBtn.style.borderBottomWidth = unbanBtn.style.borderLeftWidth = 1;
                unbanBtn.style.borderTopLeftRadius = unbanBtn.style.borderTopRightRadius =
                    unbanBtn.style.borderBottomLeftRadius = unbanBtn.style.borderBottomRightRadius = 3;
                unbanBtn.style.paddingLeft = unbanBtn.style.paddingRight = 8;
                row.Add(unbanBtn);

                scroll.Add(row);
            }
        }
    }
}
