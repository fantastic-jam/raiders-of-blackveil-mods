# Wildguard Mod Framework (WMF)

The mod framework for Raiders of Blackveil. WMF gives players in-game control over their mods and gives mod authors a common API for discovery, game modes, and settings menus — with no engine restarts required.

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

1. Download `WildguardModFramework-x.x.x.zip` from the [releases page](https://github.com/fantastic-jam/raiders-of-blackveil-mods/releases?q=WildguardModFramework).
2. Extract the ZIP into your game's `BepInEx` folder. `ModRegistry.dll` is bundled inside as a patcher — no separate download needed.
3. Launch the game.

---

## Languages

WMF's own UI (mod menu title, Mods button, host setup steppers, game mode labels, solo game screen) is available in **English** and **French**. The active language follows your in-game language setting — no extra configuration required.

Mods built on WMF can provide their own translation files using the same `TranslationService`. See the [For Mod Authors](#for-mod-authors) section for details.

---

## Mod Discovery

A **Mods** button is added to the main menu and the in-game pause menu. Opening it shows every installed BepInEx plugin. Mods that support enable/disable have a working toggle — turn them off permanently or bring them back without touching the filesystem. Mods without toggle support are listed as well, so you always have a clear picture of what's loaded.

The enabled/disabled state is saved to a config file and applied at startup. New mods are detected automatically; uninstalled mods are cleaned up from the list.

**Multiplayer awareness** — when hosting, **Allow Mods** and **Allow Cheats** toggles appear on the host setup screen. These only govern mods that are enabled in your config. The session name is suffixed with **(cheats)** or **(modded)** so other players know exactly what they're joining before they click.

Toggle changes are available from the main menu. The in-game Mods button shows current state but disables editing so nothing breaks mid-session.

---

## Game Modes and Variants

A **Game Mode** stepper appears on the host setup screen and the solo start screen. Mods that register as game modes show up as selectable entries. Only one game mode can be active per session — selecting one disables all others. The default is **Normal** (no game mode active).

Game mode mods can expose a single entry or multiple named variants from the same plugin (e.g. "Rogue Run" and "Rogue Run — Hard"). When `IsClientRequired` is set, other players joining the session must also have the mod installed and enabled.

---

## Mod Menus

The Mods screen is split into two panels: a left bar for navigation and a right panel for content. Mods that expose a settings menu get their own named entry in the left bar — players can browse settings for each mod without leaving the game.

The menu system is aware of context: mods can disable or hide controls that are unsafe to change mid-session when the menu is opened from the pause screen.

---

## For Mod Authors

WMF discovers mods automatically — no registration step needed. To unlock the full feature set (toggles, game modes, settings menus), implement `IModRegistrant` on your plugin class, or use the duck-typed convention if you prefer not to ship an extra DLL.

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

### Referencing ModRegistry.dll

`ModRegistry.dll` is bundled inside the WMF ZIP under `BepInEx/patchers/`. Copy it to your project and add a reference:

```xml
<ItemGroup>
  <Reference Include="ModRegistry">
    <HintPath>path\to\ModRegistry.dll</HintPath>
  </Reference>
</ItemGroup>
```

---

### Mod Discovery API

#### `string GetModType()` — **required**

Returns one of the `ModType` names as a plain string (case-insensitive). WMF uses this to decide which host toggle controls your mod.

| Return value | Effect |
|---|---|
| `"Mod"` | Shown under the **Allow Mods** host toggle |
| `"Cheat"` | Shown under the **Allow Cheats** host toggle |
| `"Cosmetics"` | Listed in the Mods page; not surfaced in host toggles |
| `"Utility"` | Listed in the Mods page; not surfaced in host toggles |
| `"GameMode"` | Registered as a selectable game mode; appears in the Game Mode stepper on the host and solo screens |

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

Human-readable display name shown in the Mods page. If absent or returns an empty string, WMF falls back to the BepInEx plugin `Name` constant.

---

#### `string GetModDescription()` — **optional**

Short description shown in the Mods page. Can be empty.

---

### Game Mode API

Return `"GameMode"` from `GetModType()` to register your mod as a selectable game mode. WMF disables all game mode mods at startup and enables only the one selected in the Game Mode stepper when the session starts.

#### `bool IsClientRequired { get; }` — **optional**

When `true`, clients joining a session with this game mode active must also have it installed and enabled. Defaults to `false` if omitted.

```csharp
public bool IsClientRequired => true;
```

#### Single-variant game mode

The simplest case: the whole mod is one selectable entry. No extra interface needed.

```csharp
[BepInPlugin(Id, Name, Version)]
public class MyGameMode : BaseUnityPlugin, IModRegistrant {
    public string GetModType() => nameof(ModType.GameMode);
    public string GetModName() => "My Game Mode";
    public string GetModDescription() => "A totally different way to play.";
    public bool Disabled { get; private set; }
    public bool IsClientRequired => true;

    public void Disable() { Disabled = true; _harmony?.UnpatchSelf(); }
    public void Enable()  { Disabled = false; _harmony?.PatchAll(); }
}
```

#### Multi-variant game mode (`IGameModeProvider`)

If one plugin exposes several selectable modes (e.g. "Rogue Run" and "Rogue Run — Hard"), implement `IGameModeProvider` alongside `IModRegistrant`. WMF registers one stepper entry per variant.

```csharp
using ModRegistry;
using System.Collections.Generic;

[BepInPlugin(Id, Name, Version)]
public class MyGameMode : BaseUnityPlugin, IModRegistrant, IGameModeProvider {
    public string GetModType() => nameof(ModType.GameMode);
    public string GetModName() => "My Game Mode";
    public string GetModDescription() => "";
    public bool Disabled { get; private set; }

    public void Disable() { Disabled = true; _harmony?.UnpatchSelf(); }
    public void Enable()  { Disabled = false; }   // EnableVariant re-patches

    // IGameModeProvider ──────────────────────────────────────────────────────
    public IReadOnlyList<GameModeVariant> GameModeVariants => new[] {
        new GameModeVariant("normal", "My Game Mode"),
        new GameModeVariant("hard",   "My Game Mode — Hard", "Harder variant"),
    };

    public void EnableVariant(string variantId) {
        // Enable() has already been called; activate the chosen variant.
        _currentVariant = variantId;
        _harmony?.PatchAll();
    }
}
```

`Enable()` is called first (mod-level setup), then `EnableVariant(variantId)` with the chosen variant ID. `Disable()` deactivates everything.

`VariantId` must be unique within the plugin. The full internal key used by WMF is `"pluginGuid::variantId"`.

Duck typing does not support `IGameModeProvider` — multi-variant game modes must reference `ModRegistry.dll` and implement the interface.

---

### Mod Menu API

#### `string MenuName { get; }` — **optional**

Returning a non-null string from this property opts your mod into the Mods menu left bar. WMF will call `OpenMenu` / `CloseMenu` when the player selects your entry.

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

`isInGameMenu` lets you disable editing controls that are unsafe to change mid-session. WMF owns the container's lifetime — do not hold a reference to it past `CloseMenu`.

---

### Networking API

WMF exposes a reliable data channel so mods can exchange messages between the host and clients during a session — without registering new Fusion stream types or touching the game's network layer directly.

All traffic is multiplexed over a single `DataStreamType` value owned by WMF. Each message is prefixed with its channel name so handlers only see messages addressed to them. Delivery is guaranteed (Fusion reliable data).

> **Scope** — `Broadcast` only reaches players in `WmfNetwork.ConfirmedPlayers`: clients that completed the WMF handshake. Players without WMF installed are never in that set.

#### Subscribing to a channel

Subscribe in `Enable()` and unsubscribe in `Disable()` to avoid receiving messages when your mod is off:

```csharp
using WildguardModFramework.Network;

private Action<PlayerRef, byte[]> _handler;

public void Enable() {
    _handler = OnMessage;
    WmfNetwork.Subscribe("my-mod.sync", _handler);
    // ...
}

public void Disable() {
    WmfNetwork.Unsubscribe("my-mod.sync", _handler);
    // ...
}

private void OnMessage(PlayerRef sender, byte[] payload) {
    // deserialize payload and act on it
}
```

Channel names are arbitrary UTF-8 strings (max 255 bytes). Use a namespaced format (`"mod-name.purpose"`) to avoid collisions with other mods.

---

#### Sending messages

Three delivery directions are available:

| Method | Direction | Notes |
|---|---|---|
| `WmfNetwork.Send(target, channel, payload)` | Host → specific client | Call only from the host (`runner.IsServer`) |
| `WmfNetwork.SendToHost(channel, payload)` | Client → host | Call from any client |
| `WmfNetwork.Broadcast(channel, payload)` | Host → all WMF clients | Only reaches confirmed WMF players |

```csharp
// Client sends a request to the host
var payload = Encoding.UTF8.GetBytes("hello");
WmfNetwork.SendToHost("my-mod.request", payload);

// Host replies to a specific client
WmfNetwork.Send(playerRef, "my-mod.reply", Encoding.UTF8.GetBytes("ack"));

// Host pushes state to all WMF clients
WmfNetwork.Broadcast("my-mod.state", SerializeState());
```

All payloads are raw `byte[]` — serialization format is your choice (UTF-8 strings, JSON, `BinaryWriter`, etc.).

---

#### IsServer guard

Always check `runner.IsServer` (or `NetworkRunner.IsServer`) before calling `Send` or `Broadcast`. Those methods reach out to `NetworkManager` which throws if called from a non-host context.

```csharp
if (NetworkManager.Instance?.Runner?.IsServer == true) {
    WmfNetwork.Broadcast("my-mod.tick", payload);
}
```

---

### Without a DLL reference (duck typing)

Referencing `ModRegistry.dll` is optional. WMF also discovers mods by matching method names via reflection — no interface needed. Your plugin class just needs to expose the right members with the exact signatures above:

| Member | Required |
|---|---|
| `public string GetModType()` | Yes |
| `public void Disable()` | Yes |
| `public bool Disabled { get; }` | Yes |
| `public void Enable()` | No |
| `public string GetModName()` | No |
| `public string GetModDescription()` | No |
| `public bool IsClientRequired { get; }` | No (GameMode only) |
| `public string MenuName { get; }` | No |
| `public void OpenMenu(VisualElement, bool)` | No (required if MenuName is set) |
| `public void CloseMenu()` | No (required if MenuName is set) |

Duck typing does not support `IGameModeProvider` — multi-variant game modes must reference `ModRegistry.dll` and implement the interface.

The interface approach is preferred because it gives you compile-time checks and IDE auto-complete. Duck typing is useful when you can't or don't want to ship an extra DLL alongside your mod.

---

### Localisation

WMF ships a `TranslationService` that mods can use to localise their own UI strings. Place flat JSON files under `Assets/Localization/` in your mod's output folder, named `[ModName].[lang].json` (e.g. `MyMod.en.json`, `MyMod.fr.json`).

Format — a flat key/value object:

```json
{
  "my_key": "My English string",
  "another_key": "Another string"
}
```

`TranslationService` loads all matching files at startup and picks the file that matches the player's in-game language setting. English is the fallback when no file exists for the active language.

WMF uses this same mechanism for its own UI strings — `WMF.en.json` and `WMF.fr.json` are bundled in the ZIP and cover all WMF UI text out of the box.
