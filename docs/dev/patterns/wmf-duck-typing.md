# WMF Duck Typing

WildguardModFramework (WMF) can discover mods via reflection rather than requiring them to explicitly implement `IModRegistrant`. This is called **duck typing** — if the plugin class has the right public methods, WMF treats it as a registrant without a compile-time interface reference.

## Scope: discovery only

Duck typing is limited to basic mod discovery (appearing in the WMF Mods list with a toggle). Any mod that calls a WMF API **must** compile against `WildguardModFramework.dll` and declare the dependency:

```csharp
[BepInDependency("io.github.fantastic-jam.raidersofblackveil.mods.wildguard-mod-framework")]
```

This collapses the compatibility matrix to two states — *has WMF* or *doesn't have the mod* — and lets BepInEx enforce load order. It makes the "mod without WMF" state impossible for any mod that needs WMF features.

The APIs that require a WMF reference are: game modes (`IGameModeProvider`, `GameModeVariant`), networking (`WmfNetwork`), and notifications (`WmfNotifications`, `NotificationLevel`).

## Why use duck typing instead of `IModRegistrant`?

- No `ProjectReference` to `ModRegistry` required — keeps the mod self-contained.
- No `[BepInDependency]` on WMF — the mod loads and runs standalone even if WMF isn't installed.
- WMF's `ModManager` scans every loaded BepInEx plugin by reflection and hooks any that expose the expected surface.

## Required surface

Implement these as ordinary `public` instance members on the plugin class. The names must match exactly.

| Member | Kind | Expected return / type |
|---|---|---|
| `GetModType()` | method | `string` — one of `"Mod"`, `"Cheat"`, `"GameMode"`, `"Cosmetics"`, `"Utility"` |
| `GetModName()` | method | `string` |
| `GetModDescription()` | method | `string` |
| `Enable()` | method | `void` |
| `Disable()` | method | `void` |
| `Disabled` | property (get) | `bool` |
| `IsClientRequired` | property (get) | `bool` |

WMF calls `Disable()` before `BeginPlaySession` when the host disallows a mod type, and `Enable()` when it is allowed. The game starts with all mods enabled; WMF only calls `Disable()` when it needs to suppress one.

## Patch lifecycle

**Resolve reflection handles once in `Awake()`.** Never inside `Enable()` — handles do not change between enable/disable cycles.

**Patch in `Enable()`, unpatch in `Disable()`.** Use a `_patched` guard so `Enable()` is idempotent.

```csharp
// Patch/[ModName]Patch.cs
internal static class [ModName]Patch {
    private static Harmony _harmony;
    private static bool _patched;
    internal static bool Disabled;

    // Called once from Awake() — resolve reflection handles only, no patches.
    // Returns false if any critical handle is missing (caller should abort).
    internal static bool Init() {
        // _someField = AccessTools.Field(typeof(SomeClass), "_someField");
        // if (_someField == null) {
        //     [ModName]Mod.PublicLogger.LogWarning("[ModName]: SomeClass._someField not found.");
        // }
        return true;
    }

    // Called by Mod.Enable() — idempotent.
    internal static void Patch(Harmony harmony) {
        if (_patched) { return; }
        _harmony = harmony;
        // harmony.Patch(...);
        [ModName]Mod.PublicLogger.LogInfo("[ModName] patch applied.");
        _patched = true;
    }

    // Called by Mod.Disable().
    internal static void Unpatch() {
        _harmony?.UnpatchSelf();
        _patched = false;
    }
}
```

```csharp
// [ModName]Mod.cs
[BepInPlugin(Id, Name, Version)]
public class [ModName]Mod : BaseUnityPlugin {
    private const string Id   = "io.github.fantastic-jam.raidersofblackveil.mods.[modname-lowercase]";
    public const string Name    = "[ModName]";
    public const string Version = "0.1.0";
    public const string Author  = "christphe";

    public static ManualLogSource PublicLogger;
    private readonly Harmony _harmony = new Harmony(Id);

    // WMF duck-typing — WMF discovers these via reflection; no ModRegistry reference needed.
    public string GetModType()        => "Mod";
    public string GetModName()        => Name;
    public string GetModDescription() => "";
    public bool   IsClientRequired    => false;
    public bool   Disabled            => [ModName]Patch.Disabled;

    public void Enable() {
        [ModName]Patch.Disabled = false;
        [ModName]Patch.Patch(_harmony);
    }

    public void Disable() {
        [ModName]Patch.Disabled = true;
        [ModName]Patch.Unpatch();
    }

    private void Awake() {
        PublicLogger = Logger;
        try {
            if (![ModName]Patch.Init()) {
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
```

## Sending notifications 

Any mod can push a corner notification to all WMF players in the session by broadcasting on the `"wmf.notification"` channel. WMF subscribes to that channel and displays the message locally; **older WMF versions that do not subscribe silently drop the message — they never crash.**

```csharp
using WildguardModFramework;
using WildguardModFramework.Notifications;

WmfNotifications.Notify("Install JoinAnytime!", NotificationLevel.Info, autoClose: true);
```

`Notify()` shows the notification locally and broadcasts to all WMF clients if the caller is the host.

See [API.md](../../mods/WildguardModFramework/API.md#notifications-api) for the full reference.

---

## Rules

- **No `BepInDependency` on WMF.** Duck-typed mods are fully optional WMF participants.
- **`Init()` returns `bool`.** Return `false` only if a critical reflection handle is missing and the mod cannot function at all. Missing non-critical handles should warn and continue.
- **`Enable()` is called in `Awake()` to default to active.** WMF may call `Disable()` later if needed.
- **`Disable()` unpatches** — patch methods do not need to check a `Disabled` flag because they won't run when unpatched.
- **Unpatch is idempotent** — `_harmony?.UnpatchSelf()` is safe to call when nothing is patched.
