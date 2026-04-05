# ModManager

Gives the host control over which mod types are active for a session. When hosting, two extra toggles appear in the host setup screen — **Allow Mods** and **Allow Cheats** — each showing the list of active mods in that category on hover. Setting either to **No** disables those mods before the session starts.

The session name is automatically suffixed with **(cheats)** or **(modded)** so other players can see at a glance what kind of run they're joining.

Mods opt in by implementing the `IModRegistrant` interface (or the equivalent duck-typed convention) from the [ModRegistry](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=ModRegistry) library.

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

To make your mod appear in ModManager, implement `IModRegistrant` on your plugin class. ModManager scans all loaded BepInEx plugins on startup and discovers any that match the contract below.

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

Returns one of the `ModType` names as a plain string (case-insensitive). ModManager uses this to decide which toggle controls your mod.

| Return value | Effect |
|---|---|
| `"Mod"` | Shown under the **Allow Mods** toggle |
| `"Cheat"` | Shown under the **Allow Cheats** toggle |
| `"Cosmetics"` | Tracked but not surfaced in session checkboxes (future feature) |
| `"Utility"` | Tracked but not surfaced in session checkboxes (future feature) |

Use `nameof(ModType.Mod)` etc. to get compile-time safety when referencing `ModRegistry.dll`.

---

#### `void Disable()` — **required**

Called by ModManager just before the play session begins, when the host has toggled off this mod's category. Must make the mod inert — no further game-state changes.

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

Human-readable display name shown in the ModManager UI. If absent or returns an empty string, ModManager falls back to the BepInEx plugin `Name` constant.

```csharp
public string GetModName() => "My Mod";
```

---

#### `string GetModDescription()` — **optional**

Short description shown on hover in the ModManager UI. Can be empty.

```csharp
public string GetModDescription() => "Increases gold drop rate.";
```

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
