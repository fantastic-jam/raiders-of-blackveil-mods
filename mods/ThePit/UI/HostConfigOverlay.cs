using System;
using RR.Input;
using RR.UI.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace ThePit.UI {
    // Full-screen overlay shown in the lobby when the host interacts with the planning table.
    // Stepper option lists are loaded from BepInEx config at construction time; parse errors
    // fall back to the built-in defaults. Disables all player input while visible.
    internal sealed class HostConfigOverlay {
        // ── Built-in defaults (used when cfg is absent or unparseable) ────────────

        private static readonly (string Label, float Seconds)[] DefaultDurations = {
            ("2 min",  120f),
            ("5 min",  300f),
            ("10 min", 600f),
            ("15 min", 900f),
            ("20 min", 1200f),
        };

        private static readonly (string Label, float IntervalMult)[] DefaultDropRates = {
            ("Sluggish", 3.0f),
            ("Slow",    2.0f),
            ("Normal",  1.0f),
            ("Fast",    0.67f),
            ("Rapid",   0.5f),
            ("Frenzy",  0.33f),
        };

        private static readonly (string Label, int Count)[] DefaultInitialPerks;
        private static readonly (string Label, int Level)[] LevelOptions;

        private static readonly (string Label, float MaxFactor)[] DefaultDamageReduction = {
            ("Off",     1f),
            ("Gentle",  5f),
            ("Medium",  10f),
            ("Strong",  20f),
            ("Extreme", 40f),
        };

        static HostConfigOverlay() {
            DefaultInitialPerks = new (string, int)[12];
            for (int i = 0; i < 12; i++) {
                DefaultInitialPerks[i] = ((i + 1).ToString(), i + 1);
            }
            LevelOptions = new (string, int)[20];
            for (int i = 0; i < 20; i++) {
                LevelOptions[i] = ((i + 1).ToString(), i + 1);
            }
        }

        // ── Saved session state (persists across overlay open/close, seeded from config) ──

        private static int _savedDurationIdx;
        private static int _savedDropRateIdx;
        private static int _savedInitialPerksIdx;
        private static int _savedDamageReductionIdx;
        private static int _savedInitialLevelIdx;

        // ── Instance ──────────────────────────────────────────────────────────────

        private readonly VisualElement _backdrop;
        private readonly Stepper<float> _durationStepper;
        private readonly Stepper<float> _dropRateStepper;
        private readonly Stepper<int> _initialPerksStepper;
        private readonly Stepper<float> _damageReductionStepper;
        private readonly Stepper<int> _initialLevelStepper;
        private readonly Action _onConfirm;

        internal bool IsVisible { get; private set; }

        internal HostConfigOverlay(VisualElement pageRoot, Action onConfirm) {
            _onConfirm = onConfirm;

            // Load options from cfg, falling back to built-in defaults on parse error.
            var durations = StepperOptions.ParseFloat(
                ThePitMod.CfgDurationOptions?.Value ?? string.Empty, DefaultDurations);
            var dropRates = StepperOptions.ParseFloat(
                ThePitMod.CfgDropRateOptions?.Value ?? string.Empty, DefaultDropRates);
            var initialPerks = StepperOptions.ParseInt(
                ThePitMod.CfgInitialPerksOptions?.Value ?? string.Empty, DefaultInitialPerks);
            var damageReduction = StepperOptions.ParseFloat(
                ThePitMod.CfgDamageReductionOptions?.Value ?? string.Empty, DefaultDamageReduction);

            // Resolve saved indices by matching the persisted value against the option list.
            // Null means "not set" or "value no longer in list" — falls back to hard-coded default index.
            var prefs = ThePitPrefs.Load();
            _savedDurationIdx = (prefs.DurationSeconds.HasValue ? FindIndex(durations, prefs.DurationSeconds.Value) : null) ?? 1;
            _savedDropRateIdx = (prefs.DropRateMultiplier.HasValue ? FindIndex(dropRates, prefs.DropRateMultiplier.Value) : null) ?? 2;
            _savedInitialPerksIdx = (prefs.InitialPerksCount.HasValue ? FindIndex(initialPerks, prefs.InitialPerksCount.Value) : null) ?? 5;
            _savedDamageReductionIdx = (prefs.DamageReductionFactor.HasValue ? FindIndex(damageReduction, prefs.DamageReductionFactor.Value) : null) ?? 0;
            _savedInitialLevelIdx = (prefs.InitialLevel.HasValue ? FindIndex(LevelOptions, prefs.InitialLevel.Value) : null) ?? 4;

            _durationStepper = new Stepper<float>("DURATION", durations, _savedDurationIdx);
            _dropRateStepper = new Stepper<float>("DROP SPEED", dropRates, _savedDropRateIdx);
            _initialPerksStepper = new Stepper<int>("INITIAL PERKS", initialPerks, _savedInitialPerksIdx);
            _damageReductionStepper = new Stepper<float>("DMG REDUCTION", damageReduction, _savedDamageReductionIdx);
            _initialLevelStepper = new Stepper<int>("INITIAL LEVEL", LevelOptions, _savedInitialLevelIdx);

            _backdrop = Build();
            _backdrop.style.display = DisplayStyle.None;
            pageRoot.Add(_backdrop);
        }

        internal void Show() {
            _durationStepper.SetIndex(_savedDurationIdx);
            _dropRateStepper.SetIndex(_savedDropRateIdx);
            _initialPerksStepper.SetIndex(_savedInitialPerksIdx);
            _damageReductionStepper.SetIndex(_savedDamageReductionIdx);
            _initialLevelStepper.SetIndex(_savedInitialLevelIdx);
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
            _savedDamageReductionIdx = _damageReductionStepper.Index;
            _savedInitialLevelIdx = _initialLevelStepper.Index;

            new ThePitPrefs {
                DurationSeconds = _durationStepper.Value,
                DropRateMultiplier = _dropRateStepper.Value,
                InitialPerksCount = _initialPerksStepper.Value,
                DamageReductionFactor = _damageReductionStepper.Value,
                InitialLevel = _initialLevelStepper.Value,
            }.Save();

            ThePitState.MatchDurationSecondsOverride = _durationStepper.Value;
            ThePitState.DropIntervalMultiplier = _dropRateStepper.Value;
            ThePitState.InitialChestRoundsOverride = _initialPerksStepper.Value;
            ThePitState.DamageReductionMaxFactor = _damageReductionStepper.Value;
            ThePitState.InitialLevelOverride = _initialLevelStepper.Value;

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
            card.Add(_initialLevelStepper.Root);
            card.Add(_damageReductionStepper.Root);

            var spacer = new VisualElement();
            spacer.style.height = 22;
            card.Add(spacer);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.Center;
            footer.Add(MakeDefaultButton());
            footer.Add(MakeOkButton());
            card.Add(footer);

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

        private Button MakeDefaultButton() {
            var btn = new Button(ResetToDefaults) { text = "Default" };
            btn.pickingMode = PickingMode.Position;
            btn.style.width = 100;
            btn.style.height = 36;
            btn.style.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            btn.style.fontSize = 13;
            btn.style.unityFontStyleAndWeight = FontStyle.Normal;
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
            btn.style.borderTopColor = btn.style.borderRightColor =
                btn.style.borderBottomColor = btn.style.borderLeftColor = new Color(0.25f, 0.25f, 0.25f, 1f);
            btn.style.borderTopWidth = btn.style.borderRightWidth =
                btn.style.borderBottomWidth = btn.style.borderLeftWidth = 1;
            btn.style.borderTopLeftRadius = btn.style.borderTopRightRadius =
                btn.style.borderBottomLeftRadius = btn.style.borderBottomRightRadius = 4;
            btn.style.marginRight = 8;
            return btn;
        }

        private void ResetToDefaults() {
            _durationStepper.SetIndex(1);
            _dropRateStepper.SetIndex(2);
            _initialPerksStepper.SetIndex(5);
            _damageReductionStepper.SetIndex(0);
            _initialLevelStepper.SetIndex(4);
        }

        // Returns the index of the first entry whose Value matches target, or null if not found.
        private static int? FindIndex((string Label, float Value)[] options, float target) {
            for (int i = 0; i < options.Length; i++) {
                if (Math.Abs(options[i].Value - target) < 1e-4f) { return i; }
            }
            return null;
        }

        private static int? FindIndex((string Label, int Value)[] options, int target) {
            for (int i = 0; i < options.Length; i++) {
                if (options[i].Value == target) { return i; }
            }
            return null;
        }

        private Button MakeOkButton() {
            var btn = new Button(Confirm) { text = "OK" };
            btn.pickingMode = PickingMode.Position;
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
