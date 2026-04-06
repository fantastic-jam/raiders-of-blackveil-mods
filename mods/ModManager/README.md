# ModManager

Manage and control your mods from inside the game.

A **Mods** button is added to the main menu and the in-game pause menu. Opening it shows all installed BepInEx plugins. Mods that support enable/disable (via `IModRegistrant` or duck typing) have a working toggle — turn them off permanently or bring them back on. Mods without enable/disable support are listed as well but cannot be toggled.

The Mods menu has two panels: a left bar for navigation and a right side that shows either the toggle list or a mod's own settings page. Mods that implement `IModMenuProvider` (or expose the same members via duck typing) get their own named entry in the left bar. Toggle changes are only available from the main menu — the in-game Mods button shows current state but disables editing.

The enabled/disabled state is saved to a config file and applied at startup. New mods are added automatically; uninstalled mods are cleaned up.

When hosting, the **Allow Mods** and **Allow Cheats** toggles on the host setup screen only apply to mods that are enabled in config. The session name is suffixed with **(cheats)** or **(modded)** so other players know what they're joining.

---

## Requirements

- [Raiders of Blackveil](https://store.steampowered.com/app/2246780/Raiders_of_Blackveil/)
- [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)

---

## Installation

### 1. Install BepInEx

Skip this step if BepInEx is already installed.

1. Download **BepInEx 5** from the [BepInEx releases page](https://github.com/BepInEx/BepInEx/releases) — pick the `BepInEx_win_x64` build.
2. Extract the contents into your game folder (the one containing `RoB.exe`).
3. Launch the game once and close it — BepInEx will initialize its folder structure.

### 2. Install the mod

1. Download `ModManager-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ModManager).
2. Extract the ZIP into your game's `BepInEx` folder.
3. Launch the game.

---

## For mod authors

ModManager lists every loaded BepInEx plugin. To make your mod's toggle interactive, implement `IModRegistrant` on your plugin class. ModManager scans all loaded plugins when the main menu initializes and discovers any that match the contract below.

### Quick example

```csharp
using ModRegistry;

[BepInPlugin(Id, Name, Version)]
public class MyMod : BaseUnityPlugin, IModRegistrant {
    public string GetModType() => nameof(ModType.Mod);
    public string GetModName() => "My Mod";
    public string GetModDescription() => "Does something useful.";
    public bool Disabled { get; private set; }

    public void Disable() {
        Disabled = true;
        _harmony?.UnpatchSelf();
    }

    public void Enable() {
        Disabled = false;
        _harmony?.PatchAll();
    }
}
```

### Method reference

#### `string GetModType()` — **required**

Returns one of the `ModType` names as a plain string (case-insensitive). ModManager uses this to decide which host toggle controls your mod.

| Return value | Effect |
|---|---|
| `"Mod"` | Shown under the **Allow Mods** host toggle |
| `"Cheat"` | Shown under the **Allow Cheats** host toggle |
| `"Cosmetics"` | Listed in the Mods page; not surfaced in host toggles |
| `"Utility"` | Listed in the Mods page; not surfaced in host toggles |

Use `nameof(ModType.Mod)` etc. to get compile-time safety when referencing `ModRegistry.dll`.

---

#### `void Disable()` — **required**

Called at startup when the mod is disabled in config, and just before a play session begins when the host has toggled off this mod's category. Must make the mod inert — no further game-state changes.

The recommended pattern is a static flag that all Harmony patches check at the top of their prefix/postfix:

```csharp
public void Disable() {
    Disabled = true;
    _harmony?.UnpatchSelf();    // remove patches immediately
}
```

If removing patches mid-session is unsafe for your mod, set only the flag and guard every patch method:

```csharp
[HarmonyPostfix]
static void MyPatch(...) {
    if (MyMod.Instance.Disabled) return;
    // ...
}
```

---

#### `bool Disabled { get; }` — **required**

Reflects the current disabled state. Must return `true` after `Disable()` is called and `false` after `Enable()` is called.

```csharp
public bool Disabled { get; private set; }
```

---

#### `void Enable()` — **optional**

Called when the host re-enables a mod type that was previously disabled (e.g. the player goes back to the lobby and changes the toggle). If omitted, the mod stays disabled for the rest of the session.

```csharp
public void Enable() {
    Disabled = false;
    _harmony?.PatchAll();    // restore patches
}
```

---

#### `string GetModName()` — **optional**

Human-readable display name shown in the Mods page. If absent or returns an empty string, ModManager falls back to the BepInEx plugin `Name` constant.

```csharp
public string GetModName() => "My Mod";
```

---

#### `string GetModDescription()` — **optional**

Short description. Can be empty.

```csharp
public string GetModDescription() => "Increases gold drop rate.";
```

---

---

#### `string MenuName { get; }` — **optional**

Returning a non-null string from this property opts your mod into the Mods menu left bar. ModManager will call `OpenMenu` / `CloseMenu` when the player selects your entry.

Implement `IModMenuProvider` alongside `IModRegistrant` to get compile-time safety:

```csharp
using ModRegistry;
using UnityEngine.UIElements;

public class MyMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
    // ... IModRegistrant members ...

    public string MenuName => "My Mod";

    public void OpenMenu(VisualElement container, bool isInGameMenu) {
        // Build your settings UI and add it to container.
        // isInGameMenu is true when opened from the pause menu.
    }

    public void CloseMenu() {
        // Persist or tear down settings state.
    }
}
```

`isInGameMenu` lets you disable editing controls that are unsafe to change mid-session. ModManager owns the container's lifetime — do not hold a reference to it past `CloseMenu`.

---

### Without a DLL reference (duck typing)

Referencing `ModRegistry.dll` is optional. ModManager also discovers mods by matching method names via reflection — no interface needed. Your plugin class just needs to expose the right members with the exact signatures above:

| Member | Required |
|---|---|
| `public string GetModType()` | Yes |
| `public void Disable()` | Yes |
| `public bool Disabled { get; }` | Yes |
| `public void Enable()` | No |
| `public string GetModName()` | No |
| `public string GetModDescription()` | No |
| `public string MenuName { get; }` | No |
| `public void OpenMenu(VisualElement, bool)` | No (required if MenuName is set) |
| `public void CloseMenu()` | No (required if MenuName is set) |

The interface approach is preferred because it gives you compile-time checks and IDE auto-complete. Duck typing is useful when you can't or don't want to ship an extra DLL alongside your mod.

### Referencing ModRegistry.dll

Download `ModRegistry.dll` from the [ModManager releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ModManager) and add it to your project:

```xml
<ItemGroup>
  <Reference Include="ModRegistry">
    <HintPath>path\to\ModRegistry.dll</HintPath>
  </Reference>
</ItemGroup>
```
