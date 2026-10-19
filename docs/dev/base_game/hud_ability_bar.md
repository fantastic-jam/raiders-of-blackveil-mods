# HUD ability bar (skills / upgrade bar)

`RR.UI.Controls.HUD.AbilityBar`. Verified against the 2026-10-19 build.

Since the 2026-10-19 build it derives from `Assets.Scripts.UI.Controls.ButtonBase` (was `VisualElement`) and is a
`[UxmlElement]`. `IChangeInputMethod.ChangeInputMethod` was renamed `OnChangeInputMethod`.

## Members a mod can patch

| Member | Notes |
|---|---|
| `public bool UpgradeMode { get; set; }` | Setter hides icon upgrade panels when it goes false and notifies the tutorial manager |
| `public void CheckInput(bool allowOpening)` | Called every frame by `GameHUDPage` / `LobbyHUDPage`: `UpgradeMode = allowOpening && InventoryActionMap.Ability.IsPressed()` |
| `public void SetUpgradeIndex(int newIndex, bool mouseHover = false)` | `mouseHover: true` forces `UpgradeMode = true`. Mouse hover opens the bar this way |
| `private void UpgradeAbility(int index)` | Click on an icon → `UpgradeAbility(_abilityIcons[index])` |
| `private void UpgradeAbility(AbilityIcon2 ability)` | Overload used by keyboard/gamepad shortcuts in `Update()` while `UpgradeMode` |

`_abilityIcons` is now a `List<AbilityIcon2>` (was an array), built by `GetAbilityIcon(name)` in this order:
Primary, Power, Ultimate, Special, Defensive.

## Mouse wiring (`AbilityIcon2`, also a `ButtonBase` now)

- `MouseEnterEvent` → `OnMouseEntered` → `AbilityBar.SetUpgradeIndex(index, mouseHover: true)`
- `ClickEvent` (was `RegisterLeftClickEvent`) → only when `UpgradeMode && base.AllowClick` → `OnMouseClick` → `AbilityBar.UpgradeAbility(index)`
- `ButtonBase.AllowClick` delegates to `ParentPage.AllowClick` (`IAllowClick`). It returns true and logs if `ParentPage` is null.

```csharp
var bar = AccessTools.TypeByName("RR.UI.Controls.HUD.AbilityBar");
var setIdx = AccessTools.Method(bar, "SetUpgradeIndex", new[] { typeof(int), typeof(bool) });
var upgrade = AccessTools.Method(bar, "UpgradeAbility", new[] { typeof(int) }); // the int overload only
```
