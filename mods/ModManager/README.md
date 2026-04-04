# ModManager

Gives the host control over which mods are active for a session. When hosting, two extra toggles appear in the host setup screen — **Allow Mods** and **Allow Cheats** — each showing the list of active mods in that category on hover. Setting either to **No** disables those mods before the session starts.

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

To make your mod appear in ModManager, implement `IModRegistrant` on your plugin class:

```csharp
using ModRegistry;

[BepInPlugin(Id, Name, Version)]
public class MyMod : BaseUnityPlugin, IModRegistrant {
    public string GetModType() => nameof(ModType.Mod); // Mod | Cheat | Cosmetics | Utility
    public string GetModName() => "My Mod";
    public bool Disabled { get; private set; }
    public void Disable() {
        Disabled = true;
        _harmony?.UnpatchSelf();
    }
}
```

No DLL reference needed if you prefer duck typing — any plugin exposing `GetModType()` and `Disable()` with matching signatures is picked up automatically.

See [ModRegistry](../../libs/ModRegistry/README.md) for full details.
