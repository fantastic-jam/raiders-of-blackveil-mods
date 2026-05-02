# ModRegistry

Minimal contract library for WMF mod discovery. Provides `IModRegistrant`, `IModMenuProvider`, and `ModType` — the surface WMF needs to discover, toggle, and display any mod.

Game mode support (`IGameModeProvider`, `GameModeVariant`) and notifications (`NotificationLevel`) live in `WildguardModFramework.dll` — those features require WMF to be installed, so there is no reason to keep them in a separate lib.

Referencing this DLL is **optional** — WMF also discovers mods via duck typing. Use it when you want compile-time safety and IDE support.

---

## Usage

```xml
<ItemGroup>
  <Reference Include="ModRegistry">
    <HintPath>path\to\ModRegistry.dll</HintPath>
  </Reference>
</ItemGroup>
```

```csharp
using ModRegistry;

[BepInPlugin(Id, Name, Version)]
public class MyMod : BaseUnityPlugin, IModRegistrant {
    public string GetModType() => nameof(ModType.Mod); // Mod | Cheat | Cosmetics | Utility | GameMode
    public string GetModName() => "My Mod";
    public bool Disabled { get; private set; }
    public void Disable() { Disabled = true; _harmony?.UnpatchSelf(); }
    public void Enable()  { Disabled = false; _harmony?.PatchAll(); }
}
```

### `ModType` values

| Value | Meaning |
|---|---|
| `Mod` | Gameplay-affecting mod |
| `Cheat` | Cheat / debug tool |
| `Cosmetics` | Visual/audio only |
| `Utility` | Infrastructure, no direct gameplay effect |
| `GameMode` | Selectable game mode (requires WMF reference + `IGameModeProvider`) |

---

## Duck typing (no DLL reference)

WMF picks up any plugin that exposes these members by name:

```csharp
public string GetModType()    // required — ModType name (case-insensitive)
public void Disable()         // required
public bool Disabled { get; } // required
public void Enable()          // optional
public string GetModName()    // optional
public string GetModDescription() // optional
```

For features beyond basic discovery (game modes, networking, notifications), reference `WildguardModFramework.dll` directly and add `[BepInDependency(WmfMod.Id)]`.
