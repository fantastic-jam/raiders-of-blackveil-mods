# 01 — Project Setup and WMF Registration

---

Create a BepInEx plugin, reference ModRegistry.dll, and make it appear in WMF's Mods list with a working enable/disable toggle.

## Why IModRegistrant instead of duck typing

WMF supports two ways to register a mod:

- **Duck typing** — add `GetModType()`, `GetModName()`, etc. as plain public members. No ModRegistry reference, no `[BepInDependency]` on WMF. The mod loads even if WMF isn't installed.
- **`IModRegistrant`** — implement the interface from `ModRegistry.dll`, declare `[BepInDependency]` on WMF. BepInEx enforces load order and refuses to load your mod if WMF is missing.

Player Emotes will call WMF APIs directly (settings panel in tutorial 02, networking in later tutorials). The moment you reference `WildguardModFramework.dll` at compile time, you need WMF present at runtime — duck typing's optional-WMF guarantee no longer holds. Using `IModRegistrant` with `[BepInDependency]` makes that hard dependency explicit and lets BepInEx enforce it cleanly.

**Rule of thumb:** use duck typing for mods that never call a WMF API. Use `IModRegistrant` for anything that does.

## Step 1: Create the plugin class

Add a class that extends `BaseUnityPlugin`. The `[BepInPlugin]` attribute identifies your mod to BepInEx.

```csharp
using BepInEx;

namespace PlayerEmotes {
    [BepInPlugin(Id, Name, Version)]
    public class PlayerEmotesMod : BaseUnityPlugin {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.player-emotes";
        public const string Name    = "PlayerEmotes";
        public const string Version = "0.1.0";
        public const string Author  = "christphe";
    }
}
```

## Step 2: Declare the WMF dependency and reference ModRegistry

Add `[BepInDependency]` so BepInEx guarantees WMF loads first and refuses to load your mod if WMF is missing.

```csharp
[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class PlayerEmotesMod : BaseUnityPlugin {
    ...
}
```

`ModRegistry.dll` ships as a source project in this repo (`libs/ModRegistry/`). Reference it via `ProjectReference` in your `.csproj`, alongside a `ProjectReference` to WildguardModFramework (needed for later tutorials):

```xml
<ItemGroup>
  <ProjectReference Include="..\..\libs\ModRegistry\ModRegistry.csproj" />
  <ProjectReference Include="..\WildguardModFramework\WildguardModFramework.csproj">
    <Private>False</Private>
  </ProjectReference>
</ItemGroup>
```

> `<Private>False</Private>` on the WMF reference prevents copying its DLL into your output — WMF is already present in the plugins folder at runtime.

## Step 3: Implement IModRegistrant

`IModRegistrant` (from `ModRegistry.dll`) is the interface WMF uses for mod discovery and toggling.

The Patch class follows the **Init/Patch/Unpatch** split: reflection handles are resolved once in `Init()`, patches applied in `Patch()`, and torn down in `Unpatch()`. See `docs/dev/patterns/wmf-duck-typing.md` for the full pattern.

```csharp
using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ModRegistry;
using PlayerEmotes.Patch;

namespace PlayerEmotes {
    [BepInPlugin(Id, Name, Version)]
    [BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
    public class PlayerEmotesMod : BaseUnityPlugin, IModRegistrant {
        private const string Id = "io.github.fantastic-jam.raidersofblackveil.mods.player-emotes";
        public const string Name    = "PlayerEmotes";
        public const string Version = "0.1.0";
        public const string Author  = "christphe";

        public static ManualLogSource PublicLogger;
        private readonly Harmony _harmony = new Harmony(Id);

        public string GetModType()        => nameof(ModType.Cosmetics);
        public string GetModName()        => Name;
        public string GetModDescription() => "Send floating text reactions visible to all players.";
        public bool   IsClientRequired    => false;
        public bool   Disabled            { get; private set; }

        public void Enable() {
            Disabled = false;
            PlayerEmotesPatch.Patch(_harmony);
        }

        public void Disable() {
            Disabled = true;
            PlayerEmotesPatch.Unpatch();
        }

        private void Awake() {
            PublicLogger = Logger;
            try {
                if (!PlayerEmotesPatch.Init()) {
                    PublicLogger.LogError($"{Name}: init failed — mod disabled.");
                    return;
                }
                Enable();
                PublicLogger.LogInfo($"{Name} by {Author} (version {Version}) loaded.");
            }
            catch (Exception ex) {
                PublicLogger.LogError(ex);
            }
        }
    }
}
```

> **`GetModType()`** controls which host toggle governs your mod. `nameof(ModType.Cosmetics)` appears under **Allow Cosmetics**. Other accepted values: `ModType.Mod`, `ModType.Cheat`, `ModType.Utility`, `ModType.GameMode`.

> **`Enable()` and `Disable()`** are called by WMF at startup based on the player's saved config, and again whenever the host changes mod permissions mid-session. `Awake()` should only bind config and call `Init()` — do not patch there.

> **`Disabled`** is owned by the plugin class, not the patch class. The patch class has no `Disabled` field because it does not need one — patch methods don't run when unpatched.

And the skeleton patch class (`Patch/PlayerEmotesPatch.cs`):

```csharp
using HarmonyLib;

namespace PlayerEmotes.Patch {
    internal static class PlayerEmotesPatch {
        private static Harmony _harmony;
        private static bool _patched;

        internal static bool Init() {
            return true;
        }

        internal static void Patch(Harmony harmony) {
            if (_patched) { return; }
            _harmony = harmony;
            PlayerEmotesMod.PublicLogger.LogInfo("PlayerEmotes patch applied.");
            _patched = true;
        }

        internal static void Unpatch() {
            _harmony?.UnpatchSelf();
            _patched = false;
        }
    }
}
```

## Result

Launch the game. Open the **Mods** button in the main menu or pause screen. Player Emotes appears in the list with a working enable/disable toggle.

---

## Next

→ [02-settings-panel.md](02-settings-panel.md) — Add an in-game settings panel with a configurable keybind
