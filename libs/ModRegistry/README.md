# ModRegistry

Shared contract library for [ModManager](../../mods/ModManager/README.md). Provides the `IModRegistrant` interface and `ModType` enum so mods can declare themselves to ModManager in a strongly-typed way.

Referencing this DLL is **optional** — ModManager also discovers mods via duck typing (matching method names by convention). Use this library when you want compile-time safety and IDE support.

---

## Usage

### 1. Reference the DLL

Add a project reference or drop `ModRegistry.dll` next to your mod's DLL in `BepInEx/plugins/`.

In your `.csproj`:

```xml
<ItemGroup>
  <Reference Include="ModRegistry">
    <HintPath>path\to\ModRegistry.dll</HintPath>
  </Reference>
</ItemGroup>
```

### 2. Implement the interface

```csharp
using ModRegistry;

[BepInPlugin(Id, Name, Version)]
public class MyMod : BaseUnityPlugin, IModRegistrant {
    public string GetModType() => nameof(ModType.Mod); // or Cheat, Cosmetics, Utility
    public string GetModName() => "My Mod";
    public bool Disabled { get; private set; }
    public void Disable() {
        Disabled = true;
        _harmony?.UnpatchSelf();
    }
}
```

### `ModType` values

| Value | Meaning |
|-------|---------|
| `Mod` | Gameplay-affecting mod |
| `Cheat` | Cheat / debug tool |
| `Cosmetics` | Visual/audio only |
| `Utility` | Infrastructure, no direct gameplay effect |

### Optional members

`GetModName()` and `GetModDescription()` have default implementations (empty string). If omitted or empty, ModManager falls back to the BepInEx plugin name and an empty description respectively.

---

## Duck typing (no DLL reference)

If you'd rather not take a dependency on this DLL, ModManager will still pick up your mod if your plugin class exposes these members by name:

```csharp
public string GetModType()   // required — must return one of the ModType names
public void Disable()        // required
public bool Disabled { get; } // required
public string GetModName()   // optional
public string GetModDescription() // optional
```
