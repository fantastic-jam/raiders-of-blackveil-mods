# 02 — Settings Panel

---

Prerequisite: [01-init.md](01-init.md)

Add Player Emotes to WMF's settings sidebar so players can configure the emote keybind without leaving the game.

## Step 1: IModMenuProvider

`IModMenuProvider` is in `ModRegistry.dll` — already referenced in tutorial 01. Add it to your class declaration alongside `IModRegistrant`.

```csharp
using ModRegistry;
using UnityEngine.UIElements;

[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class PlayerEmotesMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {

    // IModRegistrant members (unchanged from tutorial 01)
    ...

    // IModMenuProvider
    public string MenuName => "Player Emotes";

    public void OpenMenu(VisualElement container, bool isInGameMenu) {
        _menu = new PlayerEmotesMenu(CfgEmoteKey, isInGameMenu);
        _menu.Build(container);
    }

    public void CloseMenu() {
        _menu?.Dispose();
        _menu = null;
    }

    // Return null if you have no sub-sections.
    public (string Title, Action<VisualElement, bool> Build)[] SubMenus => null;
}
```

> **`SubMenus`** is a required `IModMenuProvider` member. It exposes optional expandable sub-sections under `MenuName`. Return `null` (or an empty array) when not used. Requires `using System;` for `Action`.

> **`CloseMenu`** should call `Dispose()` on the menu instance so any active rebind operation is cancelled cleanly when the player navigates away.

> **`UnityEngine.UIElementsModule`** must be referenced in your `.csproj` — it is not included by `Common.props`:
> ```xml
> <Reference Include="UnityEngine.UIElementsModule">
>   <HintPath>$(RaidersOfBlackveilRootPath)\RoB_Data\Managed\UnityEngine.UIElementsModule.dll</HintPath>
>   <Private>False</Private>
> </Reference>
> ```

## Step 2: Store the keybind in BepInEx config

Bind the config entry before the try/catch block so it is always available, even if `Init()` aborts.

```csharp
public static ConfigEntry<Key> CfgEmoteKey;
private PlayerEmotesMenu _menu;

private void Awake() {
    PublicLogger = Logger;
    CfgEmoteKey = Config.Bind("Controls", "EmoteKey", Key.T, "Key to send an emote.");
    try {
        // … Init, Enable, log …
    }
    catch (Exception ex) { PublicLogger.LogError(ex); }
}
```

> Use `Key.T` as the default — `Key.E` is already bound to interact in the base game.

## Step 3: Build the settings UI

Create `UI/PlayerEmotesMenu.cs`. Use `OptionButton` and `LocLabel` from `RR.UI.Controls` — the same game components used by all other WMF settings panels. `Assembly-CSharp` (already in `Common.props`) provides them, no extra csproj reference needed.

```csharp
using BepInEx.Configuration;
using RR.Input;
using RR.UI.Controls;
using RR.UI.Controls.Menu.Options;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UIElements;

namespace PlayerEmotes.UI {
    internal sealed class PlayerEmotesMenu {
        private readonly ConfigEntry<Key> _keyEntry;
        private readonly bool _isInGameMenu;
        private LocLabel _btnLabel;
        private InputActionRebindingExtensions.RebindingOperation _rebindOp;
        private InputAction _tempAction;

        internal PlayerEmotesMenu(ConfigEntry<Key> keyEntry, bool isInGameMenu) {
            _keyEntry = keyEntry;
            _isInGameMenu = isInGameMenu;
        }

        internal void Build(VisualElement container) {
            var row = new OptionButton();
            var lbl = row.Q<LocLabel>("Label");
            if (lbl != null) { lbl.CustomTransform = _ => "Emote Key"; lbl.Refresh(); }

            _btnLabel = row.Q<LocLabel>("Button");
            RefreshButtonLabel();

            if (!_isInGameMenu) {
                row.OnClickedEvent = StartRebind;
            } else {
                row.SetEnabled(false);
            }

            container.Add(row);
        }

        internal void Dispose() {
            if (_rebindOp != null) {
                _rebindOp.Cancel();
                // FinishRebind will be called via OnCancel
            }
        }

        private void RefreshButtonLabel() => SetButtonText(_keyEntry.Value.ToString());

        private void SetButtonText(string text) {
            if (_btnLabel == null) { return; }
            _btnLabel.CustomTransform = _ => text;
            _btnLabel.Refresh();
        }
    }
}
```

> **`OptionButton`** is a pre-styled game control with a label slot on the left and a button slot on the right. Query the slots by name: `"Label"` for the description, `"Button"` for the current value.

> **`LocLabel.CustomTransform`** bypasses the localization lookup and displays a literal string. Call `Refresh()` after setting it to redraw.

> **`row.SetEnabled(false)`** greys out the row when opened from the pause screen, instead of silently ignoring clicks. This matches the game's own control conventions.

## Step 4: Implement key capture

Add `StartRebind` and `FinishRebind` inside `PlayerEmotesMenu`. Disable `InputManager.UIActionMap` while listening so the UI doesn't intercept the key before the rebind operation can.

```csharp
private void StartRebind() {
    if (_rebindOp != null) { return; }

    SetButtonText("...");

    if (InputManager.Instance != null) {
        InputManager.Instance.UIActionMap.Enabled = false;
    }

    _tempAction = new InputAction("PE_Rebind", InputActionType.Button);
    _tempAction.AddBinding("<Keyboard>/t");

    _rebindOp = _tempAction
        .PerformInteractiveRebinding(0)
        .WithControlsHavingToMatchPath("<Keyboard>")
        .WithCancelingThrough("<Keyboard>/escape")
        .WithControlsExcluding("<Keyboard>/escape")
        .WithControlsExcluding("<Keyboard>/enter")
        .OnMatchWaitForAnother(0.1f)
        .OnComplete(op => FinishRebind(op, complete: true))
        .OnCancel(op => FinishRebind(op, complete: false))
        .Start();
}

private void FinishRebind(InputActionRebindingExtensions.RebindingOperation op, bool complete) {
    if (complete && op.selectedControl is KeyControl kc) {
        _keyEntry.Value = kc.keyCode;
    }

    op.Dispose();
    _rebindOp = null;
    _tempAction?.Disable();
    _tempAction?.Dispose();
    _tempAction = null;

    if (InputManager.Instance != null) {
        InputManager.Instance.UIActionMap.Enabled = true;
    }

    RefreshButtonLabel();
}
```

> **Dispose `_tempAction`** after the rebind completes. Keeping it alive leaks an enabled InputAction that may interfere with subsequent bindings.

## Result

Open **Mods → Player Emotes** from the main menu. Click the button next to **Emote Key** and press any key — the button updates immediately. The row is greyed out when opened from the pause screen. The setting persists to disk automatically.

---

## Next

→ [03-localization.md](03-localization.md) — Localize the settings panel label
