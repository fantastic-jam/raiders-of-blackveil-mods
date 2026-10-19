# UI page lifecycle (`PageBase`, HUD pages)

Applies to the game version from the 2026-10-19 update onward. Source: `game-src/RR.UI.UISystem/PageBase.cs`, `PageLoader.cs`, `RR.UI.Pages/BaseHUDPage.cs`.

## What changed in the 2026-10-19 update

| Before | After |
|---|---|
| `protected virtual void OnInit()` called from `PageBase.Construct(...)` | Removed. Pages build their UI in the **constructor** `PageBase(string name, PageArgs args)` |
| `public abstract string Name { get; }` | `public readonly string Name` (set by ctor) |
| `public void Update()` (runs `_updateItems` then `OnUpdate()`) | `public void OnUpdateBase()` |
| `public virtual void ChangeInputMethod(InputDeviceType)` | `public void OnChangeInputMethodBase(...)` + `protected virtual void OnChangeInputMethod(...)` |
| `IChangeInputMethod.ChangeInputMethod` | `IChangeInputMethod.OnChangeInputMethod` |
| `BaseHUDPage.AbilityBar/OverlayUI/...` were `{ get; private set; }` properties | `public readonly` fields |
| `UIManager.ChangePage("Name")` | `UIManager.ChangePage(typeof(PageType))` |
| `AppManager.Instance.LocalSettings` | `AppManager.Instance.Settings.Local` |

Unchanged: `protected virtual void OnUpdate()`, `OnActivate()`, `OnDeactivate()`, `OnReady()`, `RootElement`.

## Construction

`PageLoader<T>.Construct()` does `Activator.CreateInstance(typeof(T), new PageArgs { RootLayer, ParentLayer })`.
Concrete pages expose a single-arg ctor, e.g. `LobbyHUDPage(PageArgs)` / `GameHUDPage(PageArgs)`, which chain to
`BaseHUDPage(string name, PageArgs args)` → `PageBase(string name, PageArgs args)`.
`PageBase`'s ctor loads `UI/Pages/<Name>` and sets `RootElement` before any derived ctor body runs.

`PageLoader.USE_UNLOADING` is `false` and never set, so pages are constructed once and kept for the session (`Deconstruct()` is not called in practice).

## Hooking "page initialised" from a mod

Postfix the constructor. A postfix on the `BaseHUDPage` ctor runs inside the derived ctor's `base(...)` call: `RootElement` is ready and `__instance is LobbyHUDPage` already works (runtime type is set), but derived-class fields are not yet assigned.

```csharp
var hudCtor = AccessTools.Constructor(typeof(BaseHUDPage), new[] { typeof(string), typeof(PageArgs) });
harmony.Patch(hudCtor, postfix: new HarmonyMethod(AccessTools.Method(typeof(MyPatch), nameof(OnHUDCtorPostfix))));

// Need derived-class fields? Patch the concrete ctor instead:
var lobbyCtor = AccessTools.Constructor(typeof(LobbyHUDPage), new[] { typeof(PageArgs) });

static void OnHUDCtorPostfix(BaseHUDPage __instance) => MyController.OnPageInit(__instance);
```

Per-frame hooks: postfix `BaseHUDPage.OnUpdate` (both HUD pages call `base.OnUpdate()`), or `PageBase.OnUpdateBase` for every page.

## Buttons / `ParentPage`

`ButtonBase` (`Assets.Scripts.UI.Controls`) is the new base of `OptionControl` (and so of `OptionKeyBind`). Clicks now check `ButtonBase.AllowClick`, which reads `ParentPage.AllowClick`; `PageBase.GetElement<T>` sets `ParentPage` automatically. A control created by a mod (`new OptionKeyBind()`) has `ParentPage == null`, so `AllowClick` returns `true` (it logs `"ParentPage not set for button."` via `RRLog` on each click). Set `ParentPage` to the hosting page if one is available.

## Dynamic sub-pages (`UIDynamicPageLayer<T>`)

`new UIDynamicPageLayer<TPage>(PageBase parentPage, VisualElement container, float transitionSpeed = 0.3f, float backgroundOpacity = 0.85f)`
(was `(VisualElement container, UIPageLayer parentPageLayer, ...)`). It builds the page with
`Activator.CreateInstance(typeof(TPage), new PageArgs { RootLayer = parentPage.RootLayer, ParentLayer = this })`, so a mod page
must expose a **public** `(PageArgs)` ctor. `TPage : new()` is gone. `IPlayerInputControl`/`IDynamicPage` became `IDynamicPageLayer`.

```csharp
internal sealed class MyPage : PageBase {
    public MyPage(PageArgs args) : base("MenuStartHostPage" /* UXML under UI/Pages/ */, args) {
        GetElement<ButtonPopup>("StartButton", ElementUpdate.InputChange).OnClicked = _ => { /* ... */ };
    }
}
var layer = new UIDynamicPageLayer<MyPage>(ownerPage, containerElement, 0f, 0f);
```

The owner page normally calls the protected `RegisterDynamicPage(layer)` so the layer gets `Update()` / input-change /
scene callbacks. A mod-created layer is not registered, so it only works with `TransitionAnimation.None` (no fade ticking)
and the mod must forward `OnNavigateInput` itself.

## Main menu cursor (`MenuStartPage`)

`_cursor` is `UICursorLinear<ButtonMenuStart>`. `private void ResetCursor(bool showContinue)` clears it and re-registers
Continue/Join/Host/Solo/Tutorial/Settings/Exit on **every** `OnActivate()` and `OnReady()`, so items a mod registers once are lost.
Postfix `ResetCursor` to re-register. `ButtonMenuStart` is now abstract; instantiate `ButtonMenuStartNormal`.

Do not call `UICursorLinear<T>.UnregisterItem`: it removes `_items[SelectedIndex]` instead of the found index and throws
when nothing is selected (`SelectedIndex == -1`). To insert at a position, `RegisterItem` (it wires mouse hover) then move the
last entry inside the private `List<CursorStopItem> _items`.

`ButtonShared.LabelCustomTransform` was removed. Set `CustomTransform` on its inner `LocLabel` named `"Label"`, then call `Refresh()`.
