using System;
using System.Collections.Generic;
using System.Linq;
using WildguardModFramework.Registry;
using WildguardModFramework.Ui;
using RR.Input;
using RR.UI.Controls.Menu.Options;
using RR.UI.UISystem;
using RR.UI.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace WildguardModFramework.ModMenu {
    internal sealed class ModsMenuOverlay {
        private readonly VisualElement _root;

        // Left bar cursor (navigates section entries) — managed, see NavCursor.
        private readonly NavCursor _leftCursor = new();
        // Right cursor (navigates the WMF toggle list — native game controls)
        private readonly UICursorLinear<Il2CppSystem.Object> _rightCursor = new(CursorInputType.ArrowsAndStickOnly);

        private bool _leftFocused = true;

        // Left bar
        private NavButton _wmfBtn;
        private readonly List<(RegisteredMod Mod, Action<bool> SetActive)> _menuEntries = new();
        private readonly Dictionary<RegisteredMod, ExpandableNavEntry> _expandable = new();

        // Labels that need refreshing when language changes
        private Label _titleLabel;
        private Label _hintLabel;
        private Label _emptyLabel;

        // Right panels
        private VisualElement _toggleListPanel;
        private VisualElement _modSettingsContainer;

        // Managed mod toggle refresh
        private readonly List<(string guid, OptionOnOffSwitch toggle)> _toggles = new();

        // Currently open mod settings (null = WMF toggle list is shown)
        private RegisteredMod _activeMenuMod;

        private readonly bool _isInGameMenu;

        internal bool IsOpen { get; private set; }

        internal ModsMenuOverlay(VisualElement pageRoot, bool isInGameMenu = false) {
            _isInGameMenu = isInGameMenu;
            _root = Build();
            _root.style.display = DisplayStyle.None;
            pageRoot.Add(_root);
        }

        internal void Open() {
            RefreshTranslations();
            foreach (var (guid, toggle) in _toggles) {
                toggle.Value = WmfConfig.IsEnabled(guid);
            }

            _root.style.display = DisplayStyle.Flex;
            IsOpen = true;
            // Start on left bar if there are menu entries, otherwise go straight to right
            if (_menuEntries.Count > 0) {
                _leftFocused = true;
                _leftCursor.ResetSelection();
            } else {
                _leftFocused = false;
                _rightCursor.ResetSelection(true);
            }
        }

        private void RefreshTranslations() {
            if (_titleLabel != null) { _titleLabel.text = WmfMod.t("menu.title"); }
            if (_hintLabel != null) { _hintLabel.text = WmfMod.t("menu.hint.close"); }
            if (_emptyLabel != null) { _emptyLabel.text = WmfMod.t("menu.empty"); }
            _wmfBtn?.UpdateText(WmfMod.t("label.mods"));
        }

        internal void Close() {
            _activeMenuMod?.CloseMenu();
            _activeMenuMod = null;
            _root.style.display = DisplayStyle.None;
            IsOpen = false;
        }

        internal void OnNavigateInput(InputPressEvent evt) {
            if (_leftFocused) {
                _leftCursor.OnNavigateInput(evt);
                if (!evt.IsPressed) { return; }
                switch (evt.Type) {
                    case PageNavType.Cancel:
                        Close();
                        break;
                    case PageNavType.Submit:
                        _leftCursor.SelectedItem?.Submit();
                        break;
                    case PageNavType.NavigateRight:
                    case PageNavType.TabRight:
                        FocusRight();
                        break;
                }
            } else {
                _rightCursor.OnNavigateInput(evt);
                if (!evt.IsPressed) { return; }
                switch (evt.Type) {
                    case PageNavType.Cancel:
                    case PageNavType.NavigateLeft:
                    case PageNavType.TabLeft:
                        if (_menuEntries.Count > 0) {
                            FocusLeft();
                        } else {
                            Close();
                        }

                        break;
                    case PageNavType.Submit:
                        _rightCursor.SelectedItem?.Element?.Submit();
                        break;
                }
            }
        }

        // ── Panel switching ───────────────────────────────────────────────

        private void SelectWmfEntry() {
            if (_activeMenuMod != null) {
                _activeMenuMod.CloseMenu();
                _activeMenuMod = null;
                _modSettingsContainer.Clear();
            }
            _modSettingsContainer.style.display = DisplayStyle.None;
            _toggleListPanel.style.display = DisplayStyle.Flex;

            _wmfBtn.Active = true;
            foreach (var (_, setActive) in _menuEntries) { setActive(false); }
        }

        private void SelectModEntry(RegisteredMod mod) {
            if (_activeMenuMod == mod) { return; }

            if (_activeMenuMod != null) {
                _activeMenuMod.CloseMenu();
                _modSettingsContainer.Clear();
            } else {
                _toggleListPanel.style.display = DisplayStyle.None;
            }

            _activeMenuMod = mod;
            mod.OpenMenu(_modSettingsContainer, _isInGameMenu);
            _modSettingsContainer.style.display = DisplayStyle.Flex;

            _wmfBtn.Active = false;
            foreach (var (m, setActive) in _menuEntries) { setActive(m == mod); }
        }

        private void SelectSubMenu(RegisteredMod mod, int subIndex) {
            if (_activeMenuMod != null) {
                _activeMenuMod.CloseMenu();
                _modSettingsContainer.Clear();
            } else {
                _toggleListPanel.style.display = DisplayStyle.None;
            }

            _activeMenuMod = mod;
            mod.SubMenus[subIndex].Build(_modSettingsContainer, _isInGameMenu);
            _modSettingsContainer.style.display = DisplayStyle.Flex;

            _wmfBtn.Active = false;
            foreach (var (m, setActive) in _menuEntries) { setActive(m == mod); }
            if (_expandable.TryGetValue(mod, out var entry)) { entry.SetActiveChild(subIndex); }
        }

        private void FocusLeft() {
            _leftFocused = true;
            _leftCursor.ResetSelection();
        }

        private void FocusRight() {
            _leftFocused = false;
            _rightCursor.ResetSelection(true);
        }

        // ── Build ─────────────────────────────────────────────────────────

        private VisualElement Build() {
            var dividerColor = new Color(0.22f, 0.22f, 0.22f, 1f);

            var root = new VisualElement();
            root.style.position = Position.Absolute;
            root.style.top = root.style.right = root.style.bottom = root.style.left = 0;
            root.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.97f);
            root.style.flexDirection = FlexDirection.Column;

            // ── Title bar ─────────────────────────────────────────────────
            var titleBar = new VisualElement();
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.justifyContent = Justify.SpaceBetween;
            titleBar.style.alignItems = Align.Center;
            titleBar.style.paddingTop = titleBar.style.paddingBottom = 14;
            titleBar.style.paddingLeft = titleBar.style.paddingRight = 28;
            titleBar.style.borderBottomColor = dividerColor;
            titleBar.style.borderBottomWidth = 1;

            _titleLabel = new Label { text = WmfMod.t("menu.title") };
            _titleLabel.style.color = Color.white;
            _titleLabel.style.fontSize = 18;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.letterSpacing = 1;

            _hintLabel = new Label { text = WmfMod.t("menu.hint.close") };
            _hintLabel.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            _hintLabel.style.fontSize = 12;

            titleBar.Add(_titleLabel);
            titleBar.Add(_hintLabel);

            // ── Body ──────────────────────────────────────────────────────
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.overflow = Overflow.Hidden;

            // Left bar
            var leftBar = new VisualElement();
            leftBar.style.width = 220;
            leftBar.style.borderRightColor = dividerColor;
            leftBar.style.borderRightWidth = 1;
            leftBar.style.paddingTop = 12;
            leftBar.style.paddingLeft = leftBar.style.paddingRight = 8;

            // "Mods" entry — always first
            _wmfBtn = new NavButton(WmfMod.t("label.mods")) { Active = true, OnClickedEvent = SelectWmfEntry };
            _leftCursor.Register(_wmfBtn);
            leftBar.Add(_wmfBtn.Root);

            // One entry per mod that has a custom settings menu
            _menuEntries.Clear();
            _expandable.Clear();
            foreach (var mod in ModScanner.AllMods().Where(m => m.MenuName != null)) {
                if (mod.SubMenus?.Length > 0) {
                    var entry = new ExpandableNavEntry(mod.MenuName);
                    _leftCursor.Register(entry);
                    var capturedMod = mod;
                    for (var i = 0; i < mod.SubMenus.Length; i++) {
                        var idx = i;
                        var child = entry.AddChild(mod.SubMenus[i].Title, () => SelectSubMenu(capturedMod, idx));
                        _leftCursor.Register(child);
                    }
                    entry.OnExpanded = () => _leftCursor.Select(entry.Children[0]);
                    _menuEntries.Add((mod, v => { entry.HeaderActive = v; if (!v) { entry.ClearChildActive(); } }));
                    _expandable[mod] = entry;
                    leftBar.Add(entry.Root);
                } else {
                    var btn = new NavButton(mod.MenuName);
                    var capturedMod = mod;
                    btn.OnClickedEvent = () => SelectModEntry(capturedMod);
                    _leftCursor.Register(btn);
                    _menuEntries.Add((mod, v => btn.Active = v));
                    leftBar.Add(btn.Root);
                }
            }

            // Right side — two panels, one visible at a time
            var rightSide = new VisualElement();
            rightSide.style.flexGrow = 1;
            rightSide.style.position = Position.Relative;

            // Toggle list (WMF panel)
            _toggleListPanel = BuildToggleList();
            rightSide.Add(_toggleListPanel);

            // Mod settings container (cleared and repopulated on entry switch)
            _modSettingsContainer = new VisualElement();
            _modSettingsContainer.style.flexGrow = 1;
            _modSettingsContainer.style.display = DisplayStyle.None;
            _modSettingsContainer.style.paddingTop = 16;
            _modSettingsContainer.style.paddingLeft = _modSettingsContainer.style.paddingRight = 28;
            rightSide.Add(_modSettingsContainer);

            body.Add(leftBar);
            body.Add(rightSide);

            root.Add(titleBar);
            root.Add(body);

            return root;
        }

        private VisualElement BuildToggleList() {
            var panel = new ScrollView(ScrollViewMode.Vertical);
            panel.style.flexGrow = 1;
            panel.style.paddingTop = 16;
            panel.style.paddingLeft = panel.style.paddingRight = 28;

            _toggles.Clear();

            var allMods = ModScanner.AllMods().ToList();
            foreach (var mod in allMods) {
                if (mod.Guid == WmfMod.Id) { continue; } // WMF appears in left nav, not in the toggle list
                var guid = mod.Guid;
                var modEntry = new VisualElement();
                modEntry.style.marginBottom = 4;

                var toggle = new OptionOnOffSwitch {
                    LabelKey = "@" + mod.Name,
                    OnLabelKey = "@On",
                    OffLabelKey = "@Off",
                };

                if (mod.IsManaged && !_isInGameMenu) {
                    toggle.Value = WmfConfig.IsEnabled(guid);
                    var capturedMod = mod;
                    toggle.OnValueChangedCallback = (Action<bool>)(v => {
                        WmfConfig.GetEntry(guid).Value = v;
                        if (v) { capturedMod.Enable(); } else { capturedMod.Disable(); }
                    });
                    _toggles.Add((guid, toggle));
                    _rightCursor.RegisterItem(toggle.Cast<ICursorLinearStopElement>());
                } else {
                    toggle.Value = mod.IsManaged ? WmfConfig.IsEnabled(guid) : true;
                    toggle.SetEnabled(false);
                }

                modEntry.Add(toggle);

                if (!string.IsNullOrEmpty(mod.Description)) {
                    var desc = new Label { text = mod.Description };
                    desc.style.color = new Color(0.55f, 0.55f, 0.55f, 1f);
                    desc.style.fontSize = 11;
                    desc.style.paddingLeft = 10;
                    desc.style.marginBottom = 6;
                    desc.style.whiteSpace = WhiteSpace.Normal;
                    modEntry.Add(desc);
                }

                panel.Add(modEntry);
            }

            if (allMods.Count == 0) {
                _emptyLabel = new Label { text = WmfMod.t("menu.empty") };
                _emptyLabel.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                _emptyLabel.style.paddingTop = 12;
                panel.Add(_emptyLabel);
            }

            return panel;
        }

        // ── Left-bar navigation (managed) ─────────────────────────────────
        //
        // IL2CPP: game interfaces (ICursorLinearStopElement) become classes in the interop
        // assemblies, so a managed VisualElement subclass can no longer implement them.
        // The left bar therefore uses a small managed cursor over composed VisualElements
        // instead of the game's UICursorLinear.

        private abstract class NavItem {
            internal readonly VisualElement Root = new();
            internal bool Enabled = true;
            internal NavCursor Cursor;
            internal abstract bool Hovered { get; set; }
            internal abstract void Submit();

            protected void WireMouse(VisualElement target, bool stopClickPropagation = false) {
                target.On<ClickEvent>(evt => {
                    if (stopClickPropagation) { evt.StopPropagation(); }
                    Submit();
                });
                target.On<MouseEnterEvent>(_ => Cursor?.Select(this));
                target.On<MouseLeaveEvent>(_ => Cursor?.ClearSelection());
            }
        }

        private sealed class NavCursor {
            private readonly List<NavItem> _items = new();
            private int _selected = -1;

            internal NavItem SelectedItem => _selected >= 0 ? _items[_selected] : null;

            internal void Register(NavItem item) {
                item.Cursor = this;
                _items.Add(item);
            }

            internal void Select(NavItem item) => SetSelection(_items.IndexOf(item));

            internal void ClearSelection() => SetSelection(-1);

            internal void ResetSelection() => SetSelection(-1);

            internal void OnNavigateInput(InputPressEvent evt) {
                if (!evt.IsPressed) { return; }
                switch (evt.Type) {
                    case PageNavType.NavigateDown:
                        Step(+1);
                        break;
                    case PageNavType.NavigateUp:
                        Step(-1);
                        break;
                }
            }

            private void Step(int dir) {
                if (_items.Count == 0) { return; }
                var i = _selected < 0 ? (dir > 0 ? -1 : _items.Count) : _selected;
                for (var n = 0; n < _items.Count; n++) {
                    i += dir;
                    if (i < 0 || i >= _items.Count) { return; }
                    if (_items[i].Enabled) {
                        SetSelection(i);
                        return;
                    }
                }
            }

            private void SetSelection(int index) {
                if (index == _selected) { return; }
                if (SelectedItem != null) { SelectedItem.Hovered = false; }
                _selected = index >= 0 && index < _items.Count && _items[index].Enabled ? index : -1;
                if (SelectedItem != null) { SelectedItem.Hovered = true; }
            }
        }

        // ── NavButton ─────────────────────────────────────────────────────

        private sealed class NavButton : NavItem {
            private static readonly Color _activeColor = Color.white;
            private static readonly Color _inactiveColor = new(0.55f, 0.55f, 0.55f, 1f);

            private readonly Label _label;
            private bool _active;
            private bool _hover;

            public Action OnClickedEvent;

            internal override bool Hovered {
                get => _hover;
                set { _hover = value; RefreshStyle(); }
            }

            /// <summary>Permanently selected section indicator (bottom border).</summary>
            public bool Active {
                get => _active;
                set { _active = value; RefreshStyle(); }
            }

            internal override void Submit() => OnClickedEvent?.Invoke();
            internal void UpdateText(string text) => _label.text = text;

            internal NavButton(string text) {
                Root.style.paddingTop = Root.style.paddingBottom = 8;
                Root.style.paddingLeft = Root.style.paddingRight = 4;
                Root.style.borderBottomWidth = 0;
                Root.style.flexShrink = 0;

                _label = new Label { text = text };
                _label.style.fontSize = 13;
                _label.style.unityTextAlign = TextAnchor.MiddleLeft;
                Root.Add(_label);

                RefreshStyle();
                WireMouse(Root);
            }

            private void RefreshStyle() {
                _label.style.color = _active || _hover ? _activeColor : _inactiveColor;
                Root.style.borderBottomWidth = _active ? 2 : 0;
                Root.style.borderBottomColor = _active ? _activeColor : Color.clear;
                Root.style.paddingBottom = _active ? 6 : 8; // compensate 2px border
            }
        }

        // ── ExpandableNavEntry ────────────────────────────────────────────────

        private sealed class ExpandableNavEntry : NavItem {
            private static readonly Color _activeColor = Color.white;
            private static readonly Color _inactiveColor = new(0.55f, 0.55f, 0.55f, 1f);

            private readonly Label _label;
            private readonly Label _arrow;
            private readonly VisualElement _childList;
            private bool _headerActive;
            private bool _hover;

            public List<ChildNavButton> Children { get; } = new();
            public bool IsExpanded { get; private set; }

            public Action OnExpanded;

            public bool HeaderActive {
                get => _headerActive;
                set { _headerActive = value; RefreshStyle(); }
            }

            internal override bool Hovered {
                get => _hover;
                set { _hover = value; RefreshStyle(); }
            }

            internal override void Submit() {
                IsExpanded = !IsExpanded;
                _arrow.text = IsExpanded ? " ▾" : " ▸";
                _childList.style.display = IsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                foreach (var c in Children) { c.Enabled = IsExpanded; }
                if (IsExpanded) { OnExpanded?.Invoke(); }
            }

            public void SetActiveChild(int index) {
                for (var i = 0; i < Children.Count; i++) { Children[i].Active = i == index; }
            }

            public void ClearChildActive() {
                foreach (var c in Children) { c.Active = false; }
            }

            public ChildNavButton AddChild(string title, Action onClicked) {
                var child = new ChildNavButton(title, onClicked) { Enabled = false };
                Children.Add(child);
                _childList.Add(child.Root);
                return child;
            }

            internal ExpandableNavEntry(string text) {
                Root.style.flexShrink = 0;

                var headerRow = new VisualElement();
                headerRow.style.flexDirection = FlexDirection.Row;
                headerRow.style.alignItems = Align.Center;
                headerRow.style.paddingTop = headerRow.style.paddingBottom = 8;
                headerRow.style.paddingLeft = headerRow.style.paddingRight = 4;

                _label = new Label { text = text };
                _label.style.fontSize = 13;
                _label.style.unityTextAlign = TextAnchor.MiddleLeft;
                _label.style.flexGrow = 1;

                _arrow = new Label { text = " ▸" };
                _arrow.style.fontSize = 11;
                _arrow.pickingMode = PickingMode.Ignore;

                headerRow.Add(_label);
                headerRow.Add(_arrow);
                Root.Add(headerRow);

                _childList = new VisualElement();
                _childList.style.display = DisplayStyle.None;
                Root.Add(_childList);

                RefreshStyle();
                WireMouse(headerRow);
            }

            private void RefreshStyle() {
                var color = _headerActive || _hover ? _activeColor : _inactiveColor;
                _label.style.color = color;
                _arrow.style.color = color;
            }
        }

        // ── ChildNavButton ────────────────────────────────────────────────────

        private sealed class ChildNavButton : NavItem {
            private static readonly Color _activeColor = new(0.85f, 0.85f, 0.85f, 1f);
            private static readonly Color _hoverColor = Color.white;
            private static readonly Color _inactiveColor = new(0.45f, 0.45f, 0.45f, 1f);

            private readonly Label _label;
            private bool _active;
            private bool _hover;

            public Action OnClickedEvent;

            public bool Active {
                get => _active;
                set { _active = value; RefreshStyle(); }
            }

            internal override bool Hovered {
                get => _hover;
                set { _hover = value; RefreshStyle(); }
            }

            internal override void Submit() => OnClickedEvent?.Invoke();

            internal ChildNavButton(string text, Action onClicked) {
                OnClickedEvent = onClicked;
                Root.style.paddingTop = Root.style.paddingBottom = 6;
                Root.style.paddingLeft = 16;
                Root.style.paddingRight = 4;
                Root.style.flexShrink = 0;

                _label = new Label { text = text };
                _label.style.fontSize = 12;
                _label.style.unityTextAlign = TextAnchor.MiddleLeft;
                Root.Add(_label);

                RefreshStyle();
                WireMouse(Root, stopClickPropagation: true);
            }

            private void RefreshStyle() {
                _label.style.color = _active ? _activeColor : _hover ? _hoverColor : _inactiveColor;
            }
        }
    }
}
