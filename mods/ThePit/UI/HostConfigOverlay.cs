using System;
using RR.Input;
using RR.UI.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThePit.UI {
    // Full-screen overlay shown in the lobby when the host interacts with the planning table.
    // Lets the host choose match duration, perk/XP drop rate, and initial chest rounds.
    // Disables all player input while visible; restores it on close or confirm.
    internal sealed class HostConfigOverlay {
        private static readonly (string Label, float Seconds)[] DurationOptions = {
            ("5 min",  300f),
            ("8 min",  480f),
            ("10 min", 600f),
            ("15 min", 900f),
            ("20 min", 1200f),
        };

        // Multiplier on the BepInEx-configured interval. 1.0 = normal rate.
        // Base intervals: perk every 30 s, XP every 45 s.
        private static readonly (string Label, float IntervalMult)[] DropRateOptions = {
            ("Trickle", 3.0f),  // perk every ~90 s
            ("Slow",    2.0f),  // perk every ~60 s
            ("Normal",  1.0f),  // perk every ~30 s
            ("Fast",    0.67f), // perk every ~20 s
            ("Rapid",   0.5f),  // perk every ~15 s
            ("Frenzy",  0.33f), // perk every ~10 s
        };

        private static readonly (string Label, int Count)[] InitialPerkOptions;

        static HostConfigOverlay() {
            const int min = 1;
            const int max = 12;
            InitialPerkOptions = new (string, int)[max - min + 1];
            for (int i = 0; i <= max - min; i++) {
                int v = min + i;
                InitialPerkOptions[i] = (v.ToString(), v);
            }
        }

        // Persists the host's last choices within the session.
        private static int _savedDurationIdx = 2;    // "10 min"
        private static int _savedDropRateIdx = 2;    // "Normal"
        private static int _savedInitialPerksIdx = 5; // 6 rounds (index = value - 1)

        private readonly VisualElement _backdrop;
        private readonly Stepper<float> _durationStepper;
        private readonly Stepper<float> _dropRateStepper;
        private readonly Stepper<int> _initialPerksStepper;
        private readonly Action _onConfirm;

        internal bool IsVisible { get; private set; }

        internal HostConfigOverlay(VisualElement pageRoot, Action onConfirm) {
            _onConfirm = onConfirm;
            _durationStepper = new Stepper<float>("DURATION", DurationOptions, _savedDurationIdx);
            _dropRateStepper = new Stepper<float>("DROP RATE", DropRateOptions, _savedDropRateIdx);
            _initialPerksStepper = new Stepper<int>("INITIAL PERKS", InitialPerkOptions, _savedInitialPerksIdx);
            _backdrop = Build();
            _backdrop.style.display = DisplayStyle.None;
            pageRoot.Add(_backdrop);
        }

        internal void Show() {
            _durationStepper.SetIndex(_savedDurationIdx);
            _dropRateStepper.SetIndex(_savedDropRateIdx);
            _initialPerksStepper.SetIndex(_savedInitialPerksIdx);
            _backdrop.style.display = DisplayStyle.Flex;
            IsVisible = true;
            DisableInput();
        }

        // Called by the patch on ESC / close button — dismisses without starting.
        internal void Close() {
            _backdrop.style.display = DisplayStyle.None;
            IsVisible = false;
            RestoreInput();
        }

        private void Confirm() {
            _savedDurationIdx = _durationStepper.Index;
            _savedDropRateIdx = _dropRateStepper.Index;
            _savedInitialPerksIdx = _initialPerksStepper.Index;
            ThePitState.MatchDurationSecondsOverride = _durationStepper.Value;
            ThePitState.DropIntervalMultiplier = _dropRateStepper.Value;
            ThePitState.InitialChestRoundsOverride = _initialPerksStepper.Value;
            Close();
            _onConfirm?.Invoke();
        }

        private static void DisableInput() {
            var im = InputManager.Instance;
            if (im == null) { return; }

            im.PlayerInputEnable = PlayerInputEnableScope.Disable;
            im.SetCursorToMenuMode(true);
        }

        private static void RestoreInput() {
            var im = InputManager.Instance;
            if (im == null) { return; }

            im.PlayerInputEnable = PlayerInputEnableScope.Allow;
            im.SetCursorToMenuMode(false);
        }

        // ── Build ─────────────────────────────────────────────────────────────────

        private VisualElement Build() {
            var backdrop = new VisualElement();
            backdrop.pickingMode = PickingMode.Position;
            backdrop.style.position = Position.Absolute;
            backdrop.style.top = backdrop.style.right = backdrop.style.bottom = backdrop.style.left = 0;
            backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0.75f);
            backdrop.style.alignItems = Align.Center;
            backdrop.style.justifyContent = Justify.Center;

            var card = new VisualElement();
            card.pickingMode = PickingMode.Position;
            card.style.backgroundColor = new Color(0.07f, 0.07f, 0.07f, 0.98f);
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius =
                card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 8;
            card.style.borderTopColor = card.style.borderRightColor =
                card.style.borderBottomColor = card.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            card.style.borderTopWidth = card.style.borderRightWidth =
                card.style.borderBottomWidth = card.style.borderLeftWidth = 1;
            card.style.paddingTop = card.style.paddingBottom = 28;
            card.style.paddingLeft = card.style.paddingRight = 40;
            card.style.minWidth = 420;

            // Header: title + close button
            var header = new VisualElement();
            header.pickingMode = PickingMode.Position;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 24;

            var title = new Label { text = "THE PIT — MATCH SETTINGS" };
            title.pickingMode = PickingMode.Ignore;
            title.style.color = Color.white;
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1.5f;
            title.style.flexGrow = 1;
            header.Add(title);

            header.Add(MakeCloseButton());
            card.Add(header);

            card.Add(_durationStepper.Root);
            card.Add(_dropRateStepper.Root);
            card.Add(_initialPerksStepper.Root);

            var spacer = new VisualElement();
            spacer.style.height = 22;
            card.Add(spacer);

            card.Add(MakeOkButton());
            backdrop.Add(card);
            return backdrop;
        }

        private Button MakeCloseButton() {
            var btn = new Button(Close) { text = "X" };
            btn.pickingMode = PickingMode.Position;
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

        private Button MakeOkButton() {
            var btn = new Button(Confirm) { text = "OK" };
            btn.pickingMode = PickingMode.Position;
            btn.style.alignSelf = Align.Center;
            btn.style.width = 120;
            btn.style.height = 36;
            btn.style.color = Color.white;
            btn.style.fontSize = 14;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            btn.style.borderTopColor = btn.style.borderRightColor =
                btn.style.borderBottomColor = btn.style.borderLeftColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            btn.style.borderTopWidth = btn.style.borderRightWidth =
                btn.style.borderBottomWidth = btn.style.borderLeftWidth = 1;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
            return btn;
        }
    }
}
