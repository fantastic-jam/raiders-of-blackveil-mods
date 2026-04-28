using System;
using System.Collections.Generic;
using Fusion;
using RR;
using UnityEngine;
using UnityEngine.UIElements;
using WildguardModFramework.Network;

namespace WildguardModFramework.PlayerManagement {
    /// <summary>
    /// Full-screen modal overlay listing connected players with kick/ban actions.
    /// Mounted into GameHUDPage.RootElement. Shown/hidden by PlayerManagementController.
    /// </summary>
    internal sealed class PlayerManagementOverlay {
        private readonly VisualElement _backdrop;
        private readonly VisualElement _playerList;
        private readonly Action _onClose;

        internal bool IsVisible { get; private set; }

        internal PlayerManagementOverlay(VisualElement pageRoot, Action onClose) {
            _onClose = onClose;
            _backdrop = Build(out _playerList);
            _backdrop.style.display = DisplayStyle.None;
            pageRoot.Add(_backdrop);
        }

        internal void Show(IReadOnlyList<PlayerRow> rows, bool isHost) {
            _playerList.Clear();
            foreach (var row in rows) {
                _playerList.Add(BuildRow(row, isHost));
            }
            _backdrop.style.display = DisplayStyle.Flex;
            IsVisible = true;
        }

        internal void Hide() {
            _backdrop.style.display = DisplayStyle.None;
            IsVisible = false;
        }

        // ── Build ─────────────────────────────────────────────────────────

        private VisualElement Build(out VisualElement playerList) {
            var backdrop = new VisualElement();
            backdrop.pickingMode = PickingMode.Position;
            backdrop.style.position = Position.Absolute;
            backdrop.style.top = backdrop.style.right = backdrop.style.bottom = backdrop.style.left = 0;
            backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
            backdrop.style.alignItems = Align.Center;
            backdrop.style.justifyContent = Justify.Center;
            backdrop.RegisterCallback<ClickEvent>(evt => { if (evt.target == backdrop) { _onClose(); } });

            var card = new VisualElement();
            card.pickingMode = PickingMode.Position;
            card.style.backgroundColor = new Color(0.07f, 0.07f, 0.07f, 0.98f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            card.style.borderTopColor = card.style.borderRightColor =
                card.style.borderBottomColor = card.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            card.style.borderTopWidth = card.style.borderRightWidth =
                card.style.borderBottomWidth = card.style.borderLeftWidth = 1;
            card.style.paddingTop = card.style.paddingBottom = 24;
            card.style.paddingLeft = card.style.paddingRight = 32;
            card.style.width = 520;

            // Header
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;

            var title = new Label { text = WmfMod.t("players.overlay.title") };
            title.style.color = Color.white;
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1.5f;
            title.style.flexGrow = 1;
            title.pickingMode = PickingMode.Ignore;
            header.Add(title);
            header.Add(MakeCloseButton());
            card.Add(header);

            // Player list
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = 320;
            scroll.style.marginBottom = 12;
            playerList = scroll.contentContainer;
            card.Add(scroll);

            backdrop.Add(card);
            return backdrop;
        }

        private VisualElement BuildRow(PlayerRow row, bool isHost) {
            var rowEl = new VisualElement();
            rowEl.style.flexDirection = FlexDirection.Row;
            rowEl.style.alignItems = Align.Center;
            rowEl.style.paddingTop = rowEl.style.paddingBottom = 6;
            rowEl.style.borderBottomColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            rowEl.style.borderBottomWidth = 1;

            var name = new Label { text = row.UserName };
            name.style.color = Color.white;
            name.style.fontSize = 13;
            name.style.flexGrow = 1;
            name.pickingMode = PickingMode.Ignore;
            rowEl.Add(name);

            if (row.IsModded) {
                var badge = new Label { text = "WMF" };
                badge.style.color = new Color(0.3f, 0.85f, 0.45f, 1f);
                badge.style.fontSize = 10;
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.style.marginRight = 8;
                badge.pickingMode = PickingMode.Ignore;
                rowEl.Add(badge);
            }

            if (isHost && !row.IsLocal) {
                var kickBtn = MakeActionButton(WmfMod.t("players.overlay.kick"), () => OnKick(row.Ref));
                var banBtn = MakeActionButton(WmfMod.t("players.overlay.ban"), () => OnBan(row));
                rowEl.Add(kickBtn);
                rowEl.Add(banBtn);
            }

            return rowEl;
        }

        private static void OnKick(PlayerRef playerRef) {
            var runner = PlayerManager.Instance?.Runner;
            WmfMod.PublicLogger.LogInfo($"WMF: kick {playerRef.PlayerId}, runner={(runner != null)}.");
            if (runner == null) { return; }
            runner.Disconnect(playerRef);
        }

        private static void OnBan(PlayerRow row) {
            if (row.ProfileUUID != System.Guid.Empty) {
                BanList.Add(row.ProfileUUID, row.UserName);
            }
            OnKick(row.Ref);
        }

        private Button MakeCloseButton() {
            var btn = new Button(_onClose) { text = "X" };
            btn.style.width = 24;
            btn.style.height = 24;
            btn.style.fontSize = 12;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = Color.clear;
            btn.style.borderTopWidth = btn.style.borderRightWidth =
                btn.style.borderBottomWidth = btn.style.borderLeftWidth = 0;
            return btn;
        }

        private static Button MakeActionButton(string label, Action onClick) {
            var btn = new Button(onClick) { text = label };
            btn.style.height = 26;
            btn.style.fontSize = 11;
            btn.style.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            btn.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            btn.style.borderTopColor = btn.style.borderRightColor =
                btn.style.borderBottomColor = btn.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f, 1f);
            btn.style.borderTopWidth = btn.style.borderRightWidth =
                btn.style.borderBottomWidth = btn.style.borderLeftWidth = 1;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 3;
            btn.style.marginLeft = 6;
            btn.style.paddingLeft = btn.style.paddingRight = 8;
            return btn;
        }
    }

    internal readonly struct PlayerRow {
        internal readonly PlayerRef Ref;
        internal readonly System.Guid ProfileUUID;
        internal readonly string UserName;
        internal readonly bool IsModded;
        internal readonly bool IsLocal;

        internal PlayerRow(PlayerRef playerRef, System.Guid profileUuid, string userName, bool isModded, bool isLocal) {
            Ref = playerRef;
            ProfileUUID = profileUuid;
            UserName = userName;
            IsModded = isModded;
            IsLocal = isLocal;
        }
    }
}
