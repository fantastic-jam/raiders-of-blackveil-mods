# WMF Mod Author API

Reference for mod authors building on Wildguard Mod Framework.

---

## Referencing the libraries

### ModRegistry.dll — basic mod discovery

`ModRegistry.dll` is bundled inside the WMF ZIP under `BepInEx/patchers/`. It provides `IModRegistrant`, `IModMenuProvider`, and `ModType` — the minimum surface to appear in the WMF Mods list with a working toggle.

```xml
<ItemGroup>
  <Reference Include="ModRegistry">
    <HintPath>path\to\ModRegistry.dll</HintPath>
  </Reference>
</ItemGroup>
```

### WildguardModFramework.dll — full feature set

Game modes, networking, and notifications require WMF itself. Reference the DLL and declare the BepInEx dependency so load order is guaranteed:

```xml
<ItemGroup>
  <Reference Include="WildguardModFramework">
    <HintPath>path\to\WildguardModFramework.dll</HintPath>
  </Reference>
</ItemGroup>
```

```csharp
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class MyMod : BaseUnityPlugin, IModRegistrant { ... }
```

With `[BepInDependency]` declared, BepInEx refuses to load your mod if WMF is absent — which is the correct behavior since your mod's features depend on it.

---

## Mod Discovery API

WMF discovers mods automatically — no registration step needed. To unlock the full feature set (toggles, game modes, settings menus), implement `IModRegistrant` on your plugin class, or use the [duck-typed convention](#without-a-dll-reference-duck-typing) if you prefer not to ship an extra DLL.

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

---

### `string GetModType()` — **required**

Returns one of the `ModType` names as a plain string (case-insensitive). WMF uses this to decide which host toggle controls your mod.

| Return value | Effect |
|---|---|
| `"Mod"` | Shown under the **Allow Mods** host toggle |
| `"Cheat"` | Shown under the **Allow Cheats** host toggle |
| `"Cosmetics"` | Listed in the Mods page; not surfaced in host toggles |
| `"Utility"` | Listed in the Mods page; not surfaced in host toggles |
| `"GameMode"` | Registered as a selectable game mode; requires `IGameModeProvider` from `WildguardModFramework.dll` |

Use `nameof(ModType.Mod)` etc. to get compile-time safety when referencing `ModRegistry.dll`.

---

### `void Disable()` — **required**

Called at startup when the mod is disabled in config, and just before a play session begins when the host has toggled off this mod's category. Must make the mod inert — no further game-state changes.

---

### `bool Disabled { get; }` — **required**

Reflects the current disabled state. Must return `true` after `Disable()` and `false` after `Enable()`.

---

### `void Enable()` — **optional**

Called when the host re-enables a mod type that was previously disabled. If omitted, the mod stays disabled for the rest of the session.

---

### `string GetModName()` — **optional**

Human-readable display name shown in the Mods page. Falls back to the BepInEx plugin name if absent.

---

### `string GetModDescription()` — **optional**

Short description shown in the Mods page. Can be empty.

---

## Game Mode API

Return `"GameMode"` from `GetModType()` to register your mod as a selectable game mode. Game modes require `WildguardModFramework.dll` — add the reference and `[BepInDependency]` as shown in the [Referencing](#wildguardmodframeworkdll--full-feature-set) section.

### `bool IsClientRequired { get; }` — **optional**

When `true`, clients joining with this game mode active must also have it installed and enabled.

### Single-variant game mode

```csharp
using ModRegistry;
using WildguardModFramework;

[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
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

### Multi-variant game mode (`IGameModeProvider`)

If one plugin exposes several selectable modes, implement `IGameModeProvider` alongside `IModRegistrant`:

```csharp
using ModRegistry;
using WildguardModFramework;

[BepInPlugin(Id, Name, Version)]
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
public class MyGameMode : BaseUnityPlugin, IModRegistrant, IGameModeProvider {
    public string GetModType() => nameof(ModType.GameMode);
    // ... IModRegistrant members ...

    public IReadOnlyList<GameModeVariant> GameModeVariants => new[] {
        new GameModeVariant("normal", "My Game Mode"),
        new GameModeVariant("hard",   "My Game Mode — Hard", "Harder variant"),
    };

    public void EnableVariant(string variantId) {
        _currentVariant = variantId;
        _harmony?.PatchAll();
    }
}
```

`Enable()` is called first, then `EnableVariant(variantId)` with the chosen variant. `VariantId` must be unique within the plugin.

> Duck typing does not support `IGameModeProvider` — multi-variant game modes must reference `WildguardModFramework.dll`.

---

## Mod Menu API

### `string MenuName { get; }` — **optional**

Returning a non-null string opts your mod into the Mods menu left bar.

### `void OpenMenu(VisualElement container, bool isInGameMenu)` and `void CloseMenu()`

Implement `IModMenuProvider` alongside `IModRegistrant`:

```csharp
using ModRegistry;
using UnityEngine.UIElements;

public class MyMod : BaseUnityPlugin, IModRegistrant, IModMenuProvider {
    public string MenuName => "My Mod";

    public void OpenMenu(VisualElement container, bool isInGameMenu) {
        // Build settings UI. isInGameMenu = true when opened from pause menu.
    }

    public void CloseMenu() { /* persist / tear down */ }
}
```

`isInGameMenu` lets you disable controls that are unsafe to change mid-session.

### Sub-menus

```csharp
public (string Title, Action<VisualElement, bool> Build)[] SubMenus => new[] {
    ("Section A", (c, g) => BuildSectionA(c, g)),
    ("Section B", (c, g) => BuildSectionB(c, g)),
};
```

---

## Notifications API

Send a corner notification to all WMF players in the current session. Requires `WildguardModFramework.dll`.

```csharp
using WildguardModFramework;
using WildguardModFramework.Notifications;

WmfNotifications.Notify("Install Spectate Mode for better compatibility!", NotificationLevel.Info, autoClose: true);
WmfNotifications.Notify("Incompatible mod detected.", NotificationLevel.Warn, autoClose: false);
WmfNotifications.Notify("Critical sync failure.", NotificationLevel.Error, autoClose: true);
```

The message is shown locally regardless of host/client status. If the caller is the session host it is also broadcast so all other WMF clients display it.

### Via the channel (no WMF reference required)

Any mod can broadcast on `"wmf.notification"` directly. WMF versions that support notifications will display it; older installs silently ignore the message — **no crash**.

```csharp
// Payload: [1 byte: level (0=Info 1=Warn 2=Error)][1 byte: autoClose (0/1)][UTF-8 message]
static byte[] BuildPayload(string message, byte level, bool autoClose) {
    var msgBytes = Encoding.UTF8.GetBytes(message);
    var payload  = new byte[2 + msgBytes.Length];
    payload[0] = level;
    payload[1] = (byte)(autoClose ? 1 : 0);
    Buffer.BlockCopy(msgBytes, 0, payload, 2, msgBytes.Length);
    return payload;
}

if (PlayerManager.Instance?.Runner?.IsServer == true) {
    WmfNetwork.Broadcast("wmf.notification", BuildPayload("Install Spectate Mode!", 0, true));
}
```

### `NotificationLevel` values

| Value | Int | Display colour |
|---|---|---|
| `Info`  | 0 | Light grey |
| `Warn`  | 1 | Yellow |
| `Error` | 2 | Red |

### Parameters

| Parameter | Type | Default | Notes |
|---|---|---|---|
| `message` | `string` | — | Text shown in the corner |
| `level` | `NotificationLevel` | `Info` | Controls text colour |
| `autoClose` | `bool` | `true` | Dismiss after ~5 s; `false` = stays until replaced by a future close API |

> **Note:** The notification UI is a placeholder (IMGUI). A proper UIToolkit overlay with close buttons and animations will replace it in a future WMF version. The API signature is stable.

---

## Networking API

WMF exposes a reliable data channel so mods can exchange messages between the host and clients during a session. Requires `WildguardModFramework.dll`.

### Subscribing

```csharp
using WildguardModFramework.Network;

private Action<PlayerRef, byte[]> _handler;

public void Enable() {
    _handler = OnMessage;
    WmfNetwork.Subscribe("my-mod.sync", _handler);
}

public void Disable() {
    WmfNetwork.Unsubscribe("my-mod.sync", _handler);
}

private void OnMessage(PlayerRef sender, byte[] payload) { /* ... */ }
```

Channel names are arbitrary UTF-8 strings (max 255 bytes). Use a namespaced format to avoid collisions.

### Sending

| Method | Direction | Notes |
|---|---|---|
| `WmfNetwork.Send(target, channel, payload)` | Host → specific client | Requires `runner.IsServer` |
| `WmfNetwork.SendToHost(channel, payload)` | Client → host | Any context |
| `WmfNetwork.Broadcast(channel, payload)` | Host → all WMF clients | Only reaches confirmed WMF players |

Always guard `Send` and `Broadcast` with `runner.IsServer`:

```csharp
if (PlayerManager.Instance?.Runner?.IsServer == true) {
    WmfNetwork.Broadcast("my-mod.state", payload);
}
```

### Session events

```csharp
WmfNetwork.OnPlayerJoined    += (player, isModded) => { };  // host only, before handshake
WmfNetwork.OnPlayerConfirmed += (player, isModded) => { };  // host only, after handshake
WmfNetwork.OnPlayerLeft      += (player)           => { };  // all machines
```

---

## Localisation

Place flat JSON files under `Assets/Localization/` in your mod's output folder:

```
Assets/Localization/MyMod.en.json
Assets/Localization/MyMod.fr.json
```

Format — flat key/value:

```json
{ "my_key": "My English string" }
```

Usage:

```csharp
using WildguardModFramework.Translation;

var t = TranslationService.For(MyMod.Name, Info.Location);
var text = t["my_key"];
```

`TranslationService` loads the file matching the player's in-game language setting; English is the fallback.

---

## Without a DLL reference (duck typing)

WMF also discovers mods via reflection — no interface or DLL reference needed for basic discovery:

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

Duck typing covers **discovery only**. Any mod that calls into WMF APIs (game modes, networking, notifications) must reference `WildguardModFramework.dll` and declare `[BepInDependency(WmfMod.Id)]`. Duck typing does not support `IGameModeProvider`.
